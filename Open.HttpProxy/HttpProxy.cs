﻿
using System;
using System.Diagnostics;
using System.Net.Sockets;
using Open.HttpProxy.Utils;

namespace Open.HttpProxy
{
	using BufferManager;
	using EventArgs;
	using Listeners;
	
	public class HttpProxy
	{
		private readonly TcpListener _listener;
		public static EventHandler<ConnectionEventArgs> OnClientConnect;
		public static EventHandler<SessionEventArgs> OnRequest;
		public static EventHandler<SessionEventArgs> OnResponse;
		internal static TraceSource Trace = new TraceSource("Open.HttpProxy");
		internal static readonly BufferAllocator BufferAllocator = new BufferAllocator(new byte[20 * 1024 * 1024]);

		public HttpProxy(int port=8888)
		{
			_listener = new TcpListener(port);
			_listener.ConnectionRequested += OnConnectionRequested;
		}

		public void Start()
		{
			_listener.Start();
		}

		public void Stop()
		{
			_listener.Stop();
		}

		private async void OnConnectionRequested(object sender, ConnectionEventArgs e)
		{
			using (new TraceScope(Trace, "Receiving new connection from: {e.Stream.Uri}"))
			{
				var clientConnection = e.Stream;

				Events.Raise(OnClientConnect, this, new ConnectionEventArgs(clientConnection));
				var session = new Session(clientConnection);
				var stateMachine = StateMachineBuilder.Build();
				try
				{
					await stateMachine.RunAsync(session).WithoutCapturingContext();
				}
				catch (Exception ex)
				{
					Trace.TraceData(TraceEventType.Error, 0, ex);
					await session.ClientHandler.SendErrorAsync(
						ProtocolVersion.Parse("HTTP/1.1"), 
						502, "Bad Gateway", ex.ToString())
						.WithoutCapturingContext();
				}
				finally
				{
					clientConnection?.Close();
				}
			}
		}
	}
}
