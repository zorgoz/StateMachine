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
...
```C#
storeMachine.WhenException(StoreStates.WaitingForStoreKeeper);

storeMachine
	.SuperState(StoreStates._StoreKeeperServing, MemoryType.None)
	.WithSubStates(StoreStates.WorkingOnServicing, StoreStates.__AcquiringSignatures, StoreStates.BothSignaturesAcquired);

storeMachine.SuperState(StoreStates.__AcquiringSignatures, MemoryType.None)
	.WithSubStates(StoreStates.StartingAcquisition, StoreStates.ClientSignatureAcquired, StoreStates.StoreKeeperSignatureAcquired);

storeMachine
	.In(StoreStates._StoreKeeperServing)
	.Exit(ResetStore)
	.On(StoreUserActions.Reset)
	.Goto(StoreStates.WaitingForStoreKeeper);

storeMachine
	.In(StoreStates.WaitingForStoreKeeper)
	.Entry(() => serving = null)
	.On(StoreUserActions.NewOutServicingWithoutClient)
		.Goto(StoreStates.WorkingOnServicing)
		.Execute(async (t, _) =>
		{
			var e = t.On as UserActionEvent<StoreUserActions.Actions>;

			serving = new StoreState();

			using (var DC = GetDC())
			{
				serving.Cart = await DC.Carts
					.Include(x => x.User)
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.Id == (int)e.Payload && !x.IsClientEdited && x.Office == true, token)
					.ConfigureAwait(false);

				if (serving.Cart == null) throw new KeyNotFoundException($"No availalble cart with id {(int)e.Payload}");

				serving.User = serving.Cart.User;
				serving.Interactive = false;
				serving.AsVIP = false;

				var servicing = DC.Servicings.Create();
				servicing.Kind = serving.Kind = ServicingKind.OfficeLending;
				servicing.OfficeLendingState = OfficeLendingState.Preparation;
				servicing.CartId = serving.Cart.Id;
				servicing.StartedAsVip = serving.AsVIP = serving.Cart.ClientIsVip;
				servicing.StartedForUserId = serving.User.Id;
				servicing.StartedAt = DateTime.Now;
				DC.Servicings.Add(servicing);

				await DC.SaveChangesAsync(token).ConfigureAwait(false);
				serving.ServicingId = servicing.Id;

				StoreContext?.ToTransferOutPage(CatUser.ToString(serving.User), serving.AsVIP, true, serving.Cart.Message);

				logger.Info($"{storeUserName} started servicing {servicing.Kind} #{servicing.Id}");
			}
		})
	.On(StoreUserActions.NewOutServicingFromClientCart)
		.Goto(StoreStates.WorkingOnServicing)
		.Execute(async (t, _) =>
		{
			var e = t.On as UserActionEvent<StoreUserActions.Actions>;

			serving = new StoreState();

			using (var DC = GetDC())
			{
				serving.Cart = await DC.Carts
					.Include(x => x.User)
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.Id == (int)e.Payload && !x.IsClientEdited && x.Office == false, token)
					.ConfigureAwait(false);

				if (serving.Cart == null) throw new KeyNotFoundException($"No availalble cart with id {(int)e.Payload}");

				serving.User = serving.Cart.User;
				serving.Interactive = serving.User.Id == client?.Working?.Id;

				if (!serving.Interactive)
				{
					StoreShowMessage(StoreMessages.ClientPresenceNeeded);
					throw new SilentException("Cannot start lending out without the client at the panel.");
				}

				var servicing = DC.Servicings.Create();
				servicing.Kind = serving.Kind = ServicingKind.LendingOut;
				servicing.CartId = serving.Cart.Id;
				servicing.StartedAsVip = serving.AsVIP = serving.Cart.ClientIsVip;
				servicing.StartedForUserId = serving.User.Id;
				servicing.StartedAt = DateTime.Now;
				DC.Servicings.Add(servicing);

				await DC.SaveChangesAsync(token).ConfigureAwait(false);
				serving.ServicingId = servicing.Id;

				StoreContext?.ToTransferOutPage(CatUser.ToString(serving.User), serving.AsVIP, false, servicing.Cart.Message);
				ClientContext?.ToTransferPage(client.Ip, serving.User?.Firstname, serving.User?.Lastname, servicing.Kind);

				logger.Info($"{storeUserName} started servicing {servicing.Kind} #{servicing.Id}");
			}
		})
	.On(StoreUserActions.NewInServicingWithClient)
		.Goto(StoreStates.WorkingOnServicing)
		.Execute(async (_) =>
		{
			serving = new StoreState();

			using (var DC = GetDC())
			{
				serving.User = client.Working;
				serving.AsVIP = client.IsVip;
				serving.Interactive = serving.User.Id == client?.Working?.Id;

				if (!serving.Interactive)
				{
					StoreShowMessage(StoreMessages.ClientPresenceNeeded);
					throw new SilentException("Cannot start lending out without the client at the panel.");
				}

				var servicing = DC.Servicings.Create();
				servicing.Kind = serving.Kind = ServicingKind.TakingBackWithClientPresent;
				servicing.StartedAsVip = client.IsVip;
				servicing.StartedForUserId = serving.User.Id;
				servicing.StartedAt = DateTime.Now;
				DC.Servicings.Add(servicing);

				await DC.SaveChangesAsync(token).ConfigureAwait(false);
				serving.ServicingId = servicing.Id;

				StoreContext?.ToTransferInPage(CatUser.ToString(serving.User), serving.AsVIP);
				ClientContext?.ToTransferPage(client.Ip, serving.User?.Firstname, serving.User?.Lastname, servicing.Kind);

				logger.Info($"{storeUserName} started servicing {servicing.Kind} #{servicing.Id}");
			}
		})
	.On(StoreUserActions.NewInServicingWithoutClient)
		.Goto(StoreStates.WorkingOnServicing)
		.Execute(async (_) =>
		{
			serving = new StoreState();

			using (var DC = GetDC())
			{
				serving.Interactive = false;
				serving.AsVIP = false;

				var servicing = DC.Servicings.Create();
				servicing.Kind = serving.Kind = ServicingKind.TakeBackWithoutClientPresent;
				servicing.StartedAsVip = false;
				servicing.StartedForClient = null;
				servicing.StartedAt = DateTime.Now;
				DC.Servicings.Add(servicing);

				await DC.SaveChangesAsync(token).ConfigureAwait(false);
				serving.ServicingId = servicing.Id;

				StoreContext?.ToTransferInPage(string.Empty, false);

				logger.Info($"{storeUserName} started servicing {servicing.Kind} #{servicing.Id}");
			}
		})
	.On(StoreUserActions.ReopenServicing)
		.Goto(StoreStates.WorkingOnServicing)
		.Execute(async (t, _) =>
		{
			var e = t.On as UserActionEvent<StoreUserActions.Actions>;

			serving = new StoreState();

			using (var DC = GetDC())
			{
				var servicing = await DC.Servicings.FirstAsync(x => x.Id == (int)e.Payload && (x.OfficeLendingState == null || x.OfficeLendingState == OfficeLendingState.Preparation), token).ConfigureAwait(false);

				serving.ServicingId = servicing.Id;

				if (servicing == null) throw new KeyNotFoundException($"Cannot find servicing #{e.Payload} to reopen it");

				serving.Interactive = client?.Working?.Id == servicing.StartedForUserId;

				if (!serving.Interactive && servicing.Kind.NeedsInteractive())
				{
					StoreShowMessage(StoreMessages.ClientPresenceNeeded);
					throw new SilentException("Cannot reopen lending out without the same client at the panel.");
				}

				serving.User = await DC.CatUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == servicing.StartedForUserId, token).ConfigureAwait(false);
				serving.AsVIP = servicing.StartedAsVip;
				serving.Kind = servicing.Kind;

				if (serving.Interactive)
				{
					ClientContext?.ToTransferPage(client.Ip, serving.User?.Firstname, serving.User?.Lastname, servicing.Kind);
				}

				switch (servicing.Kind)
				{
					case ServicingKind.LendingOut:
						StoreContext?.ToTransferOutPage(CatUser.ToString(serving.User), serving.AsVIP, false, servicing.Cart.Message);
						break;
					case ServicingKind.OfficeLending:
						StoreContext?.ToTransferOutPage(CatUser.ToString(serving.User), serving.AsVIP, true, servicing.Cart.Message);
						break;
					case ServicingKind.TakingBackWithClientPresent:
						StoreContext?.ToTransferInPage(CatUser.ToString(serving.User), serving.AsVIP);
						break;
					case ServicingKind.TakeBackWithoutClientPresent:
						StoreContext?.ToTransferInPage(string.Empty, false);
						break;
				}

				logger.Info($"{storeUserName} reopened servicing {servicing.Kind} #{servicing.Id}");
			}
		})
		.WhenException()
		;

storeMachine
	.In(StoreStates.WorkingOnServicing)
	.Entry(async (_) =>
	{
		using (var DC = GetDC())
		{
			var servicing = await DC.Servicings.Include(x => x.Cart).FirstAsync(x => x.Id == serving.ServicingId, token).ConfigureAwait(false);

			switch (servicing.Kind)
			{
				case ServicingKind.LendingOut:
				case ServicingKind.OfficeLending:
					serving.Cart = servicing.Cart;
					var outList = await PrepareTransferOutList(DC, servicing).ConfigureAwait(false);
					StoreContext?.UpdateTransferOutList(outList);
					if (serving.Interactive) ClientContext?.UpdateTransferOutList(client.Ip, outList);
					break;

				case ServicingKind.TakingBackWithClientPresent:
					var inList = await PrepareTransferInList(DC, servicing).ConfigureAwait(false);
					StoreContext?.UpdateTransferInList(inList);
					if (serving.Interactive) ClientContext?.UpdateTransferInList(client.Ip, inList);
					break;

				case ServicingKind.TakeBackWithoutClientPresent:
					StoreContext?.UpdateTransferInList(await PrepareTransferInList(DC, servicing).ConfigureAwait(false));
					break;
			}

			logger.Debug($"Transfer lists sent out for servicing {servicing.Kind} #{servicing.Id}");
		}
	})
	.On(StoreUserActions.ModifyServicing)
		.Goto(StoreStates.WorkingOnServicing)
		.Execute(async (t, _) =>
		{
			var e = t.On as UserActionEvent<StoreUserActions.Actions>;
			var mod = e.Payload as ServicingItemModification;

			switch (mod.Action)
			{
				case Modification.Add:
					var result = await DoAddToServicing(mod).ConfigureAwait(false);
					if (result != StoreMessages.OK) StoreShowMessage(result);
					break;

				case Modification.Remove:
					await DoRemoveFromServicing(mod).ConfigureAwait(false);
					break;

				case Modification.Modify:
					await DoModifyServicing(mod).ConfigureAwait(false);
					break;
			}
		})
	.On(StoreUserActions.EndedCollecting)
		.If(() => serving.Kind != ServicingKind.OfficeLending)
		.Goto(StoreStates.StartingAcquisition)
		.Execute(() =>
		{
			if (serving.Interactive) ClientContext?.ConfirmWithCardOrDecline(client.Ip);
			StoreContext?.ConfirmWithCardOrDecline();
		})
	.On(StoreUserActions.EndedCollecting)
		.If(() => serving.Kind == ServicingKind.OfficeLending)
		.Goto()
		.Execute(async (_) =>
		{
			using (var DC = GetDC())
			{
				var servicing = await DC.Servicings.Include(x => x.Cart).FirstAsync(x => x.Id == serving.ServicingId, token).ConfigureAwait(false);
				servicing.OfficeLendingState = OfficeLendingState.Prepared;

				await DC.SaveChangesAsync(token).ConfigureAwait(false);

				logger.Debug($"Servicing #{serving.ServicingId} set to OfficeLendingOut");
			}

			StoreContext?.ToClientActivitiesSelectionPage();
		})
	;

storeMachine
	.In(StoreStates.StartingAcquisition)
	.On(CardReaderEvent.Any)
		.If(t => (t.On as CardReaderEvent)?.IsStoreKeeper == true)
		.Goto(StoreStates.StoreKeeperSignatureAcquired)
		.Execute(e => serving.SigningStoreKeeperId = (e.On as CardReaderEvent).Event.CatUserId)
	.On(CardReaderEvent.Any)
		.If(e => (e.On as CardReaderEvent)?.IsStoreKeeper == false)
		.Goto(StoreStates.ClientSignatureAcquired)
		.Execute(t => serving.SigningClientId = (t.On as CardReaderEvent).Event.CatUserId)
	;

storeMachine
	.In(StoreStates.StoreKeeperSignatureAcquired)
	.Immediately()
		.If(_ => !serving.Interactive)
		.Goto(StoreStates.BothSignaturesAcquired)
	.On(CardReaderEvent.Any)
		.If(t => (t.On as CardReaderEvent)?.IsStoreKeeper == false)
		.Goto(StoreStates.BothSignaturesAcquired)
		.Execute(t => serving.SigningClientId = (t.On as CardReaderEvent).Event.CatUserId)
	;

storeMachine
	.In(StoreStates.ClientSignatureAcquired)
	.On(CardReaderEvent.Any)
	.If(t => (t.On as CardReaderEvent)?.IsStoreKeeper == true)
		.Goto(StoreStates.BothSignaturesAcquired)
		.Execute(t => serving.SigningStoreKeeperId = (t.On as CardReaderEvent).Event.CatUserId)
	;

storeMachine
	.In(StoreStates.__AcquiringSignatures)
	.On(ClientUserActions.DeclinedConfirmation)
		.Goto(StoreStates.WorkingOnServicing)
		.Execute(() => StoreShowMessage(StoreMessages.ClientDeclined))
	.On(StoreUserActions.DeclinedConfirmation)
		.Goto(StoreStates.WorkingOnServicing)
		.Execute(() => ClientContext?.ShowMessage(client.Ip, ClientMessages.StoreKeeperDeclined))
	;

storeMachine
	.In(StoreStates.BothSignaturesAcquired)
	.Entry(async (_) =>
	{
		using (var DC = GetDC())
		{
			var servicing = await DC.Servicings.FirstAsync(x => x.Id == serving.ServicingId, token).ConfigureAwait(false);

			servicing.SigningClientUserId = serving.SigningClientId;
			servicing.SigningStoreKeeperUserId = serving.SigningStoreKeeperId;

			foreach (var t in servicing.ServicingToolItems)
			{
				if (servicing.Kind == ServicingKind.LendingOut)
				{
					t.ToolInstance.LastOutServicingToolItem = t.Id;
					t.ToolInstance.IsInStock = false;
					t.ToolInstance.IsAssigned = false;
					t.ToolInstance.LastUserId = servicing.SigningClientUserId;
				}
				else
				{
					t.ToolInstance.LastInServicingToolItem = t.Id;
					t.ToolInstance.IsInStock = true;
					t.ToolInstance.IsAssigned = false;
				}
			}

			servicing.ClosedAt = DateTime.Now;

			await DC.SaveChangesAsync(token).ConfigureAwait(false);

			StoreRefreshStatus(servicing.ServicingToolItems.GetStatusList());

			StoreContext?.ToClientActivitiesSelectionPage();
			if (serving.Interactive)
			{
				ClientContext?.ShowMessage(client.Ip, ClientMessages.Bye);
				ClientReset();
			}

			logger.Info($"{storeUserName} finished servicing {servicing.Kind} #{servicing.Id}.");
		}
	})
	.Immediately()
	.Goto(StoreStates.WaitingForStoreKeeper)
	;

storeMachine.Initialize(StoreStates.WaitingForStoreKeeper, false);

storeMachine.OnException.Subscribe(_ => StoreShowMessage(StoreMessages.Error));
```
...
```C#
public enum PortableStates
{
	/// <summary>
	/// State machine is waiting for a store keeper to initiate any transfer
	/// </summary>
	Idle = 1,

	/// <summary>
	/// Superstate representing any activity
	/// </summary>
	_Active,

	/// <summary>
	/// Operator started out transfer
	/// </summary>
	OutTransferStarted,

	/// <summary>
	/// Operator started in transfer
	/// </summary>
	InTransferStarted,

	/// <summary>
	/// Machine is waiting for user to identify himself
	/// </summary>
	WaitingForClientCard,

	/// <summary>
	/// Machine is waiting for storekeeper to submit in transfer elements
	/// </summary>
	WaitingForInSubmission,

	/// <summary>
	/// Starting signature acquisition (under __AcquiringSignatures under _StoreKeeperActive)
	/// </summary>
	StartingAcquisition,

	/// <summary>
	/// Client signature acquired (under __AcquiringSignatures under _StoreKeeperServing)
	/// </summary>
	ClientSignatureAcquired,

	/// <summary>
	/// StoreKeeper signature acquired (under __AcquiringSignatures under _StoreKeeperServing)
	/// </summary>
	StoreKeeperSignatureAcquired,

	/// <summary>
	/// Both client and store keeper signatures acquired (under _StoreKeeperActive)
	/// </summary>
	BothSignaturesAcquired,
}

internal enum StoreStates
{
	/// <summary>
	/// Machine is waiting for the store keeper to step in
	/// </summary>
	WaitingForStoreKeeper = 1,

	/// <summary>
	/// Super state representing the store keeper working on the transfer
	/// </summary>
	_StoreKeeperServing, // Super state

	/// <summary>
	/// Store keeper is collecting items (under _StoreKeeperServing)
	/// </summary>
	WorkingOnServicing,

	/// <summary>
	/// Both client and store keeper signatures acquired (under _StoreKeeperServing)
	/// </summary>
	BothSignaturesAcquired,

	/// <summary>
	/// Super state representing the signature acquisition steps (under _StoreKeeperServing)
	/// </summary>
	__AcquiringSignatures,

	/// <summary>
	/// Starting signature acquisition (under __AcquiringSignatures under _StoreKeeperServing)
	/// </summary>
	StartingAcquisition,

	/// <summary>
	/// Client signature acquired (under __AcquiringSignatures under _StoreKeeperServing)
	/// </summary>
	ClientSignatureAcquired,

	/// <summary>
	/// StoreKeeper signature acquired (under __AcquiringSignatures under _StoreKeeperServing)
	/// </summary>
	StoreKeeperSignatureAcquired,
}
```
