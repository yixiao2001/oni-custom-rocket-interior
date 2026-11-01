namespace CustomRocketInterior.Core
{
    /// <summary>
    /// Vanilla's semi-virtual wire link ignores Operational flags. Disconnect the
    /// interior plug's link when its support disappears, and reconnect on repair.
    /// This component is added only to the player-built interior power plug.
    /// </summary>
    public sealed class InteriorPowerPlugSupport : KMonoBehaviour
    {
        protected override void OnSpawn()
        {
            base.OnSpawn();
            Subscribe((int)GameHashes.OperationalFlagChanged, RefreshConnection);
            RefreshConnection(null);
        }

        protected override void OnCleanUp()
        {
            Unsubscribe((int)GameHashes.OperationalFlagChanged, RefreshConnection);
            base.OnCleanUp();
        }

        private void RefreshConnection(object ignored)
        {
            Operational operational = GetComponent<Operational>();
            if (operational != null)
                SetFoundationSupport(operational.GetFlag(RequiresFoundation.solidFoundation));
        }

        internal void SetFoundationSupport(bool supported)
        {
            GetComponent<WireUtilitySemiVirtualNetworkLink>()?.SetLinkConnected(supported);
        }
    }
}
