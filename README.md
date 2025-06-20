## No complete documentation yet, just a hint of the usage and capabilities

machine.WhenException(PortableStates.Idle);

```c#
			machine.SuperState(PortableStates._Active, MemoryType.None)
				.WithSubStates(PortableStates.ClientSignatureAcquired, PortableStates.BothSignaturesAcquired,
							   PortableStates.InTransferStarted, PortableStates.OutTransferStarted,
							   PortableStates.StartingAcquisition, PortableStates.StoreKeeperSignatureAcquired,
							   PortableStates.WaitingForClientCard, PortableStates.WaitingForInSubmission);

			machine.In(PortableStates._Active)
				.On(PortableUserActions.Reset)
				.Goto(PortableStates.Idle)
				;

			machine.In(PortableStates.Idle)
				.Entry(async (t, _)
					=> {
						if (t?.WhenException != null) orchestrator.ShowMessage(ip, PortableMessages.Error);
						await Reset().ConfigureAwait(false);
					})
				.On(PortableUserActions.Reset)
					.Goto(PortableStates.Idle)
				.On(PortableUserActions.StartedOutTransfer)
					.Goto(PortableStates.StartingAcquisition)
					.Execute(async (t, _) =>
					{
						using (var dc = getDC())
						{
							var e = t.On as PortableUserActionEvent;
							PortableOut ws = e.Payload as PortableOut;
							workingState = ws;

							portablePlaceConfigurator.AddPortablePlace(ws.ReaderId.Value);

							var servicing = await dc.Servicings.FirstAsync(x => x.Id == ws.ServicingId && x.Kind == ServicingKind.OfficeLending && x.OfficeLendingState == OfficeLendingState.Prepared, token).ConfigureAwait(false);
							servicing.OfficeLendingState = OfficeLendingState.Handling;

							await dc.SaveChangesAsync(token).ConfigureAwait(false);

							orchestrator.ConfirmWithCardOrDecline(ip);
						}
					})
				.On(PortableUserActions.InitiatedInTransfer)
					.Goto(PortableStates.WaitingForClientCard)
					.Execute(t => {
						var e = t.On as PortableUserActionEvent;
						workingState = new PortableIn { ReaderId = (int)e.Payload };

						portablePlaceConfigurator.AddPortablePlace(workingState.ReaderId.Value);

						orchestrator.ShowMessage(ip, PortableMessages.PresentCardForAuthentication);
					})
				;

			machine
				.In(PortableStates.StartingAcquisition)
				.On(CardReaderEvent.Any)
					.If(t => t.On.TestCardEvent(x => x.Event.PlaceId == workingState.ReaderId && x.IsStoreKeeper))
					.Goto(PortableStates.StoreKeeperSignatureAcquired)
					.Execute(t => workingState.SigningStoreKeeperId = (t.On as CardReaderEvent).Event.CatUserId)
				.On(CardReaderEvent.Any)
					.If(t => t.On.TestCardEvent(x => x.Event.PlaceId == workingState.ReaderId && !x.IsStoreKeeper))
					.Goto(PortableStates.ClientSignatureAcquired)
					.Execute(t => workingState.SigningClientId = (t.On as CardReaderEvent).Event.CatUserId)
				;

			machine
				.In(PortableStates.StoreKeeperSignatureAcquired)
				.On(CardReaderEvent.Any)
					.If(t => t.On.TestCardEvent(x => x.Event.PlaceId == workingState.ReaderId && !x.IsStoreKeeper))
					.Goto(PortableStates.BothSignaturesAcquired)
					.Execute(t => workingState.SigningClientId = (t.On as CardReaderEvent).Event.CatUserId)
				;

			machine
				.In(PortableStates.ClientSignatureAcquired)
				.On(CardReaderEvent.Any)
				.If(t => t.On.TestCardEvent(x => x.Event.PlaceId == workingState.ReaderId && x.IsStoreKeeper))
					.Goto(PortableStates.BothSignaturesAcquired)
					.Execute(t => workingState.SigningStoreKeeperId = (t.On as CardReaderEvent).Event.CatUserId)
				;

			machine
				.In(PortableStates.BothSignaturesAcquired)
				.Entry(async (__,_) =>
				{
					using (var dc = getDC())
					{
						if (workingState is PortableOut wso)
						{
							var servicing = await dc.Servicings
								.Include(x => x.ServicingToolItems)
								.FirstAsync(x => x.Id == wso.ServicingId && x.Kind == ServicingKind.OfficeLending && x.OfficeLendingState == OfficeLendingState.Handling, token)
								.ConfigureAwait(false);

							servicing.SigningClientUserId = wso.SigningClientId;
							servicing.SigningStoreKeeperUserId = wso.SigningStoreKeeperId;
							servicing.ClosedAt = DateTime.Now;
							servicing.OfficeLendingState = null;

							foreach (var t in servicing.ServicingToolItems)
							{
								t.ToolInstance.LastOutServicingToolItem = t.Id;
								t.ToolInstance.IsInStock = false;
								t.ToolInstance.IsAssigned = false;
								t.ToolInstance.LastUserId = servicing.SigningClientUserId;
							}

							dc.Carts.Remove(servicing.Cart);

							await dc.SaveChangesAsync(token).ConfigureAwait(false);

							storeOrchestrator.StoreRefreshStatus(servicing.ServicingToolItems.GetStatusList());

							logger.Info($"Storekpper #{wso.SigningStoreKeeperId} and client #{wso.SigningClientId} finished servicing {servicing.Kind} #{servicing.Id}.");

							orchestrator.ShowMessage(ip, PortableMessages.Finished);
						}

						if (workingState is PortableIn wsi)
						{
							using (var tr = dc.Database.BeginTransaction())
							{
								var servicing = dc.Servicings.Create();

								servicing.Kind = ServicingKind.TakingBackAtOffice;
								servicing.SigningClientUserId = servicing.StartedForUserId = wsi.SigningClientId;
								servicing.SigningStoreKeeperUserId = wsi.SigningStoreKeeperId;
								servicing.ClosedAt = servicing.StartedAt = DateTime.Now;

								await dc.SaveChangesAsync(token).ConfigureAwait(false);

								foreach (var item in wsi.Elements)
								{
									var t = dc.ServicingToolItems.Create();

									t.ToolInstance = await dc.ToolInstances.FirstAsync(x => x.Id == item.Id, token).ConfigureAwait(false);
									t.ToolInstance.LastIn = t;
									t.ToolInstance.IsInStock = true;
									t.ToolInstance.IsAssigned = false;
									t.Servicing = servicing;
									if (!string.IsNullOrWhiteSpace(item.Comment)) t.Comment = item.Comment;
								}

								await dc.SaveChangesAsync(token).ConfigureAwait(false);

								storeOrchestrator.StoreRefreshStatus(servicing.ServicingToolItems.GetStatusList());

								tr.Commit();

								logger.Info($"Storekpper #{wsi.SigningStoreKeeperId} and client #{wsi.SigningClientId} finished servicing {servicing.Kind} #{servicing.Id}.");

								orchestrator.ShowMessage(ip, PortableMessages.Finished);
							}
						}
					}
				})
					.Immediately()
					.Goto(PortableStates.Idle)
				;

			machine
				.In(PortableStates.WaitingForClientCard)
				.Entry(() => timeoutEventStream.SetTimeout(TimeSpan.FromSeconds(cardTimeoutSeconds)))
				.Exit(timeoutEventStream.Clear)
				.On(CardReaderEvent.Any)
					.If(t => t.On.TestCardEvent(x => x.Event.PlaceId == workingState.ReaderId && x.IsStoreKeeper))
					.Goto()
					.Execute(() => orchestrator.ShowMessage(ip, PortableMessages.StoreKeeperCantBeClient))
				.On(CardReaderEvent.Any)
					.If(t => t.On.TestCardEvent(x => x.Event.PlaceId == workingState.ReaderId && !x.IsStoreKeeper))
					.Goto(PortableStates.WaitingForInSubmission)
					.Execute(async (t, _) =>
					{
						using (var dc = getDC())
						{
							var e = (t.On as CardReaderEvent).Event;
							workingState.SigningClientId = e.CatUserId;
							(workingState as PortableIn).User = await dc.CatUsers.AsNoTracking().FirstAsync(x => x.Id == e.CatUserId).ConfigureAwait(false);
						}
					})
				.On(TimeoutEvent.Any)
					.Goto(PortableStates.Idle)
				;

			machine
				.In(PortableStates.WaitingForInSubmission)
				.Entry(async(__, _) =>
				{
					using (var dc = getDC())
					{
						var atClient = Mapper.Map<List<TransferInTool>>(await dc.GetLentItemsFor(workingState.SigningClientId).ToListAsync(token).ConfigureAwait(false));

						var ws = workingState as PortableIn;
						orchestrator.ToAtClientPage(ip, ws.User.Firstname, ws.User.Lastname, atClient);
					}
				})
				.On(PortableUserActions.SubmittedIntTransfer)
				.Goto(PortableStates.StartingAcquisition)
				.Execute(t =>
				{
					(workingState as PortableIn).Elements = ((t.On as PortableUserActionEvent).Payload as PortableIn).Elements;
					orchestrator.ConfirmWithCardOrDecline(ip);
				})
				;
```
