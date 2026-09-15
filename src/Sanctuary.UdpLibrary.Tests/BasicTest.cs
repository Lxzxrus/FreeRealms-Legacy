using System;
using System.Diagnostics;
using System.Threading;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.UdpLibrary.Configuration;
using Sanctuary.UdpLibrary.Enumerations;

namespace Sanctuary.UdpLibrary.Tests;

[TestClass]
public class BasicTest
{
    [TestMethod]
    public void ClientServerCommunication()
    {
        var protocolName = "Test";

        var serverParams = new UdpParams(ManagerRole.ExternalServer)
        {
            ProtocolName = protocolName,
            BindIpAddress = "127.0.0.1",
            Port = 12345
        };

        serverParams.EncryptMethod[0] = EncryptMethod.UserSupplied;
        serverParams.UserSuppliedEncryptExpansionBytes = 1;

        var clientParams = new UdpParams(ManagerRole.ExternalClient)
        {
            ProtocolName = protocolName
        };

        clientParams.EncryptMethod[0] = EncryptMethod.UserSupplied;
        clientParams.UserSuppliedEncryptExpansionBytes = 1;

        var serviceCollection = new ServiceCollection();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var serverManager = new TestManager(true, serverParams, serviceProvider);
        var clientManager = new TestManager(false, clientParams, serviceProvider);

        var stop = new CancellationTokenSource();

        Exception? serverError = null;
        Exception? clientError = null;
        var clientDisconnectReason = DisconnectReason.None;

        var serverThread = new Thread(() =>
        {
            try
            {
                ServerLoop(serverManager, stop.Token);
            }
            catch (Exception ex)
            {
                serverError = ex;
            }
        })
        {
            IsBackground = true
        };

        var clientThread = new Thread(() =>
        {
            try
            {
                clientDisconnectReason = ClientLoop(clientManager, stop.Token);
            }
            catch (Exception ex)
            {
                clientError = ex;
            }
        })
        {
            IsBackground = true
        };

        serverThread.Start();
        clientThread.Start();

        var clientFinished = clientThread.Join(TimeSpan.FromSeconds(30));

        stop.Cancel();

        serverThread.Join(TimeSpan.FromSeconds(5));
        clientThread.Join(TimeSpan.FromSeconds(5));

        Assert.IsTrue(clientFinished, "The client did not disconnect within 30 seconds.");
        Assert.IsNull(serverError, $"The server thread threw: {serverError}");
        Assert.IsNull(clientError, $"The client thread threw: {clientError}");
        Assert.AreEqual(DisconnectReason.Application, clientDisconnectReason);
    }

    private void ServerLoop(TestManager manager, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            manager.GiveTime();
    }

    private DisconnectReason ClientLoop(TestManager manager, CancellationToken cancellationToken)
    {
        var connection = manager.EstablishConnection("127.0.0.1", 12345);

        if (connection is null)
            throw new InvalidOperationException("EstablishConnection returned null.");

        while (connection.Status == Status.Negotiating && !cancellationToken.IsCancellationRequested)
            manager.GiveTime();

        while (connection.Status != Status.Disconnected && !cancellationToken.IsCancellationRequested)
        {
            manager.GiveTime();

            connection.GetStats(out var stats);

            Debug.WriteLine("{0}  AVE={1} HIGH={2} LOW={3} MSTR={4},{5} CRC={6} ORD={7}  {8}<<{9}  {10}>>{11}",
                connection,
                stats.AveragePingTime,
                stats.HighPingTime,
                stats.LowPingTime,
                stats.MasterPingTime,
                stats.MasterPingAge,
                stats.CrcRejectedPackets,
                stats.OrderRejectedPackets,
                stats.SyncOurReceived,
                stats.SyncTheirSent,
                stats.SyncOurSent,
                stats.SyncTheirReceived);
        }

        return connection.DisconnectReason;
    }
}
