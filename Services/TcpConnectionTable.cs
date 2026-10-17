using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace AutoCommand.Services
{
    /// <summary>
    /// Atomic enumeration of established IPv4 TCP connections with their
    /// owning PID, via the IP Helper API (GetExtendedTcpTable). Replaces
    /// spawning netstat.exe everywhere the monitor used it: a sampled child
    /// process shows up in every process listing (self-noise — the monitor's
    /// own netstat was once mistaken for a suspicious process), takes
    /// hundreds of milliseconds (long enough for a PID to be reused between
    /// enumeration and the name lookup that joins it), and its text output
    /// still has to be parsed. One syscall here returns the whole table with
    /// PIDs at a single kernel instant — no child process, microseconds of
    /// skew. IPv4 only (AF_INET), matching the netstat parsing it replaces;
    /// the Sysmon event path covers IPv6.
    /// </summary>
    public static class TcpConnectionTable
    {
        public sealed record Row(
            string LocalEndPoint, string RemoteEndPoint,
            string RemoteIp, int RemotePort, int Pid);

        private const int AfInet = 2;               // AF_INET
        private const int TcpTableOwnerPidAll = 5;  // TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL
        private const uint MibTcpStateEstablished = 5;
        private const uint ErrorInsufficientBuffer = 122;

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint State;
            public uint LocalAddr;
            public uint LocalPort;   // network byte order in the low two bytes
            public uint RemoteAddr;
            public uint RemotePort;  // network byte order in the low two bytes
            public uint OwningPid;
        }

        [DllImport("iphlpapi.dll")]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable, ref int dwOutBufLen, bool bOrder,
            int ulAf, int tableClass, uint reserved);

        /// <summary>
        /// All ESTABLISHED IPv4 TCP rows, including loopback (filtering is
        /// the caller's policy, same as with netstat output). Empty on any
        /// failure — callers degrade exactly the way they did when the
        /// netstat spawn failed.
        /// </summary>
        public static List<Row> GetEstablished()
        {
            var rows = new List<Row>();
            int size = 0;
            uint rc = GetExtendedTcpTable(IntPtr.Zero, ref size, false, AfInet, TcpTableOwnerPidAll, 0);
            if (rc != ErrorInsufficientBuffer || size < 4) return rows;

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (GetExtendedTcpTable(buffer, ref size, false, AfInet, TcpTableOwnerPidAll, 0) != 0)
                    return rows;

                int count = Marshal.ReadInt32(buffer);
                int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
                for (int i = 0; i < count; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(
                        buffer + 4 + (nint)(i * rowSize));
                    if (row.State != MibTcpStateEstablished) continue;

                    var localIp = new IPAddress(row.LocalAddr);
                    var remoteIp = new IPAddress(row.RemoteAddr);
                    rows.Add(new Row(
                        $"{localIp}:{Port(row.LocalPort)}",
                        $"{remoteIp}:{Port(row.RemotePort)}",
                        remoteIp.ToString(), Port(row.RemotePort), (int)row.OwningPid));
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
            return rows;
        }

        // The DWORD carries the port in network byte order: swap the low two bytes.
        private static int Port(uint networkOrder) =>
            (int)(((networkOrder & 0xFF) << 8) | ((networkOrder >> 8) & 0xFF));
    }
}
