using System.Linq;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Wieldable;

namespace Content.Shared._NF.Traits;

public sealed class OneHandParalyzedSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedHandsSystem _sharedHandsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly SharedVerbSystem _verbSystem = default!;

   ISawmill oneHandParalyzedLogger = Logger.GetSawmill("one-hand-paralyzed");

    public override void Initialize()
    {
        SubscribeLocalEvent<OneHandParalyzedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OneHandParalyzedComponent, ComponentShutdown>(OnShutdown);
        // SubscribeLocalEvent<OneHandParalyzedComponent, PickupAttemptEvent>(OnPickUpAttempt);
        SubscribeLocalEvent<OneHandParalyzedComponent, WieldAttemptEvent>(OnWieldAttempt);
        SubscribeLocalEvent<OneHandParalyzedComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<OneHandParalyzedComponent, BeforeInteractHandEvent>(BeforeInteractHand);
    }

    private void OnStartup(Entity<OneHandParalyzedComponent> ent, ref ComponentStartup args)
    {
        // Sets the paralyzed hand to the active one (currently just the right hand) only once.
        ent.Comp.ParalyzedHand ??= _sharedHandsSystem.GetActiveHand(ent.Owner);
        Dirty(ent);
    }

    // private void OnPickUpAttempt(Entity<OneHandParalyzedComponent> ent, ref PickupAttemptEvent args)
    // {
    //     if (args.Cancelled)
    //         return;
    //
    //     // Can't pick item up with paralyzed hand.
    //     var usingParalyzedHand = _sharedHandsSystem.GetActiveHand(args.User) == ent.Comp.ParalyzedHand;
    //     // Can't carry item that requires two hands if you have 2 (or less) hands and one of them is paralyzed.
    //     var itemTooBig = HasComp<MultiHandedItemComponent>(args.Item) && _sharedHandsSystem.GetHandCount(args.User) <= 2;
    //
    //     if (usingParalyzedHand || itemTooBig)
    //     {
    //         var message = Loc.GetString("trait-one-hand-paralyzed-pickup-attempt", ("item", Identity.Entity(args.Item, EntityManager)));
    //         _popupSystem.PopupClient(Loc.GetString(message), ent, ent, PopupType.SmallCaution);
    //         args.Cancel();
    //     }
    // }

    private void OnPullAttempt(Entity<OneHandParalyzedComponent> ent, ref PullAttemptEvent args)
    {
        TryComp(ent.Owner, out PullerComponent? pullerComp);
        if (args.Cancelled || !pullerComp!.NeedsHands)
            return;

        // Can't pull item with paralyzed hand.
        var usingParalyzedHand = _sharedHandsSystem.GetActiveHand(args.PullerUid) == ent.Comp.ParalyzedHand;
        // Can't pull item that requires two hands if you have 2 (or less?) hands and one of them is paralyzed.
        var itemTooBig = HasComp<MultiHandedItemComponent>(args.PulledUid) && _sharedHandsSystem.GetHandCount(args.PullerUid) <= 2;

        if (usingParalyzedHand || itemTooBig)
        {
            var message = Loc.GetString("trait-one-hand-paralyzed-pull-attempt", ("item", Identity.Entity(args.PulledUid, EntityManager)));
            _popupSystem.PopupClient(message, ent.Owner, ent.Owner, PopupType.SmallCaution);
            args.Cancelled = true;
        }
    }

    private void OnWieldAttempt(Entity<OneHandParalyzedComponent> ent, ref WieldAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        args.Cancelled = true;

        var selfMessage = Loc.GetString("trait-one-hand-paralyzed-wield-message", ("item", args.Wielded));
        var othersMessage = Loc.GetString("trait-one-hand-paralyzed-wield-message-other", ("user", Identity.Entity(args.User, EntityManager)), ("item", args.Wielded));
        _popupSystem.PopupPredicted(selfMessage, othersMessage, args.User, args.User, PopupType.SmallCaution);
    }

    private void BeforeInteractHand(Entity<OneHandParalyzedComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        var activeHand = _sharedHandsSystem.GetActiveHand(ent.Owner);
        var usingParalyzedHand = activeHand == ent.Comp.ParalyzedHand;
        if (!usingParalyzedHand)
        {
            args.Handled = false;
        }
        else
        {
            var message = "";
            var firstVerb = _verbSystem.GetLocalVerbs(args.Target, ent.Owner, typeof(InteractionVerb)).First();
            var isItem = TryComp<ItemComponent>(args.Target, out var item);

            switch (firstVerb.Text)
            {
                // Are we trying to pick up or activate an item with the paralyzed hand?
                case "Pick Up" or "Put in hand":
                    message = Loc.GetString("trait-one-hand-paralyzed-pickup-attempt",
                        ("item", Identity.Entity(args.Target, EntityManager)));
                    break;
                case "Activate":
                    message = Loc.GetString("trait-one-hand-paralyzed-activate-attempt",
                        ("item", Identity.Entity(args.Target, EntityManager)));
                    break;
                default:
                {
                    if (isItem)
                    {
                        message = Loc.GetString("trait-one-hand-paralyzed-fallback");
                    }

                    break;
                }
            }


            // If we got to this point and there's no message, run a regular InteractHandEvent on the target.
            if (message == "")
            {
                var ev = new InteractHandEvent(ent.Owner, args.Target);
                RaiseLocalEvent(args.Target, ev);
            }
            _popupSystem.PopupClient(Loc.GetString(message), ent, ent, PopupType.SmallCaution);
        }
    }
}
