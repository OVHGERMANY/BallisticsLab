using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using UnityEngine;

namespace BallisticsLab.Runtime.Bots
{
    internal sealed class BotTestController
    {
        private BotOwner _owner;
        private bool _frozen;
        private bool _wasPaused;
        private float _previousPauseEnd;
        private bool _previousCanShoot;

        internal Player SelectedPlayer { get; private set; }
        internal bool Frozen => _frozen;

        internal string SelectUnderCrosshair(GameWorld world)
        {
            Camera camera = Camera.main;
            if (world == null || camera == null)
            {
                return "No active world camera.";
            }

            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                500f,
                BallisticsCalculatorConstants.HitMask,
                QueryTriggerInteraction.Collide);

            foreach (RaycastHit hit in hits.OrderBy(value => value.distance))
            {
                BodyPartCollider bodyPart = hit.collider.GetComponent<BodyPartCollider>();
                Player player = bodyPart?.Player as Player;
                if (player == null)
                {
                    player = world.GetPlayerByCollider(hit.collider);
                }

                if (player == null || player == world.MainPlayer || !player.IsAI || player.AIData?.BotOwner == null)
                {
                    continue;
                }

                ClearSelection();
                SelectedPlayer = player;
                _owner = player.AIData.BotOwner;
                return "Selected " + (player.Profile?.Nickname ?? player.ProfileId) + ".";
            }

            return "No live AI target was under the crosshair.";
        }

        internal string Freeze()
        {
            if (SelectedPlayer == null || !SelectedPlayer.HealthController.IsAlive)
            {
                return "Select a live AI target first.";
            }

            _owner ??= SelectedPlayer.AIData?.BotOwner;
            if (_owner == null)
            {
                return "The selected AI target no longer has an active controller.";
            }

            if (!_frozen)
            {
                _wasPaused = _owner.Mover?.Pause ?? false;
                _previousPauseEnd = _owner.Mover?._pauseEndTime ?? 0f;
                _previousCanShoot = _owner.ShootData?.CanShootByState ?? true;
            }

            _frozen = true;
            MaintainFreeze();
            return "Target movement and firing are held until release or session end.";
        }

        internal string ReleaseHold()
        {
            if (_frozen
                && _owner != null
                && SelectedPlayer?.HealthController?.IsAlive == true)
            {
                if (_owner.Mover != null)
                {
                    _owner.Mover.Pause = _wasPaused;
                    _owner.Mover._pauseEndTime = _previousPauseEnd;
                    if (!_wasPaused)
                    {
                        _owner.Mover.MovementResume();
                    }
                }

                _owner.ShootData?.SetCanShootByState(_previousCanShoot);
            }

            _frozen = false;
            return SelectedPlayer == null
                ? "No bot is selected."
                : "Target hold released; selection retained for telemetry.";
        }

        internal string ClearSelection()
        {
            ReleaseHold();
            _owner = null;
            SelectedPlayer = null;
            return "Target selection cleared.";
        }

        internal void Update()
        {
            if (SelectedPlayer == null)
            {
                return;
            }

            if (!_frozen)
            {
                return;
            }

            if (_owner == null)
            {
                _frozen = false;
                return;
            }

            if (!SelectedPlayer.HealthController.IsAlive)
            {
                ReleaseHold();
                _owner = null;
                return;
            }

            MaintainFreeze();
        }

        internal string Describe()
        {
            if (SelectedPlayer == null)
            {
                return "No bot selected.";
            }

            bool alive = SelectedPlayer.HealthController.IsAlive;
            float health = SelectedPlayer.HealthController.GetBodyPartHealth(EBodyPart.Common).Current;
            List<string> armor = new List<string>();
            foreach (ArmorComponent component in SelectedPlayer.Inventory.GetPutOnArmors())
            {
                if (component?.Repairable == null)
                {
                    continue;
                }

                armor.Add(
                    "C" + component.ArmorClass + " " + component.Template.ArmorMaterial
                    + " " + component.Repairable.Durability.ToString("F1")
                    + "/" + component.Repairable.MaxDurability.ToString("F1"));
            }

            return (SelectedPlayer.Profile?.Nickname ?? SelectedPlayer.ProfileId)
                + " | HP " + health.ToString("F1")
                + " | " + (alive ? (_frozen ? "HELD" : "live") : "DEAD - selection retained")
                + (armor.Count > 0 ? " | " + string.Join("; ", armor) : " | no equipped armor");
        }

        private void MaintainFreeze()
        {
            if (_owner?.Mover != null)
            {
                _owner.Mover.MovementPause(2f, false);
                _owner.Mover.Stop();
                _owner.Mover.Sprint(false, false);
            }

            if (_owner?.ShootData != null)
            {
                _owner.ShootData.BlockFor(2f);
                _owner.ShootData.SetCanShootByState(false);
                _owner.ShootData.EndShoot();
            }
        }
    }
}
