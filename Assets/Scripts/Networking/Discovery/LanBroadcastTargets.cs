using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AMath.Networking.Discovery
{
    /// <summary>
    /// Resolves UDP broadcast endpoints for every active IPv4 interface plus the
    /// global broadcast address.
    /// </summary>
    public static class LanBroadcastTargets
    {
        /// <summary>
        /// Returns distinct broadcast endpoints for <paramref name="port"/>, including
        /// <see cref="IPAddress.Broadcast"/> and each subnet-directed address.
        /// </summary>
        public static IReadOnlyList<IPEndPoint> GetEndpoints(int port)
        {
            var endpoints = new List<IPEndPoint>();
            var seen = new HashSet<string>();

            AddEndpoint(endpoints, seen, IPAddress.Broadcast, port);

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    IPAddress broadcast = ComputeBroadcastAddress(unicast.Address, unicast.IPv4Mask);
                    if (broadcast != null)
                        AddEndpoint(endpoints, seen, broadcast, port);
                }
            }

            return endpoints;
        }

        /// <summary>
        /// Computes the subnet broadcast address from an IPv4 host address and mask.
        /// Returns null when the inputs are not IPv4.
        /// </summary>
        public static IPAddress ComputeBroadcastAddress(IPAddress address, IPAddress mask)
        {
            if (address == null || mask == null)
                return null;

            byte[] ipBytes = address.GetAddressBytes();
            byte[] maskBytes = mask.GetAddressBytes();
            if (ipBytes.Length != 4 || maskBytes.Length != 4)
                return null;

            var broadcastBytes = new byte[4];
            for (int i = 0; i < 4; i++)
                broadcastBytes[i] = (byte)(ipBytes[i] | (byte)~maskBytes[i]);

            return new IPAddress(broadcastBytes);
        }

        private static void AddEndpoint(
            List<IPEndPoint> endpoints,
            HashSet<string> seen,
            IPAddress address,
            int port)
        {
            if (address == null)
                return;

            string key = address.ToString() + ":" + port;
            if (!seen.Add(key))
                return;

            endpoints.Add(new IPEndPoint(address, port));
        }
    }
}
