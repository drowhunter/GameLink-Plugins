using IronRebellion.Properties;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using YawGLAPI;


namespace YawVR_Game_Engine.Plugin
{
	[Export(typeof(Game))]
	[ExportMetadata("Name", "Iron Rebellion")]
	[ExportMetadata("Version", "1.0")]
	public class Plugin : Game
	{
		private Random random = new Random();
		public int STEAM_ID => 1192900; // Game's SteamID. App will lauch game based on this
        public string PROCESS_NAME => "Iron Rebellion"; // The gameprocess name. App will wait/monitor this process for different features like autostart.

		public bool PATCH_AVAILABLE => false; // Tell app if patch is needed. "Patch" Button will appear -> Needed manually

		public string AUTHOR => "McFredward"; // Creator, will show this on plugin manager

		public Stream Logo => GetStream("logo.png"); // Logo for the main Library list

		public Stream SmallLogo => GetStream("recent.png"); // Logo for Library->Recent list

		public Stream Background => GetStream("wide.png"); // Wide logo for Description

		public string Description => Resources.desc;


		private IMainFormDispatcher dispatcher; // this is our reference to the app. Features like showing dialog/notification can be used
		private IProfileManager controller; // this is our reference to profile manager. input values need to be passed to this


		//We'll provide these inputs to the app.. This can even be marshalled from a struct for example
		private string[] inputNames = new string[]
		{
            "VELOCITY_X", "VELOCITY_Y", "VELOCITY_Z",
            "ANGULAR_X", "ANGULAR_Y", "ANGULAR_Z",
            "ROTATION_X", "ROTATION_Y", "ROTATION_Z",
            "TILT", "LEAN",
            "IS_FLYING", "IS_RUNNING", "IS_HIT", "WEAPON_FIRED", "STOMPED", "LANDED", "JUMPED"
        };


		private CancellationTokenSource tokenSource;
		/// <summary>
		/// Default LED profile
		/// </summary>
		public LedEffect DefaultLED()
		{
			// ask the dispatcher to convert our string to a LedEffect
			return dispatcher.JsonToLED(Resources.defaultProfile);
		}
		/// <summary>
		/// Default axis profile
		/// </summary>
		public List<Profile_Component> DefaultProfile()
		{
			// ask the dispatcher to convert our string to a axis profile
			return dispatcher.JsonToComponents(Resources.defaultProfile);
		}

		/// <summary>
		/// Will be called at plugin stop request
		/// </summary>
		public void Exit()
		{
			tokenSource?.Cancel();
		}

		/// <summary>
		/// App fetches available inputs through this
		/// </summary>
		/// <returns></returns>
		public string[] GetInputData()
		{
			return inputNames;
		}

		/// <summary>
		/// Features for the plugin. 
		/// These features can be setup to be called for different events like pushing arcade buttons
		/// </summary>
		public Dictionary<string, ParameterInfo[]> GetFeatures()
		{
			return new Dictionary<string, ParameterInfo[]>()
			{
			};
		}

		/// <summary>
		/// Will be called when app starts this plugin
		/// </summary>
		public void Init()
		{
			tokenSource = new CancellationTokenSource();
			new Thread(() =>
			{
				int port = 6969; // The UDP port to listen on

				Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); // Create a socket with SO_REUSEADDR option set
				udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				udpSocket.Bind(new IPEndPoint(IPAddress.Any, port));

				// Create a UdpClient from the existing socket
				UdpClient udpClient = new UdpClient
				{
					Client = udpSocket
				};

				IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);

				Console.WriteLine($"Listening for UDP packets on port {port}...");

				while (!tokenSource.IsCancellationRequested)
				{
					try
					{
						// Receive UDP data
						byte[] data = udpClient.Receive(ref remoteEndPoint);
						// Ensure the data has the expected length (5 floats = 20 bytes)
						if (data.Length >= 51)
						{
                            // Extract float values
                            float velocityX = BitConverter.ToSingle(data, 0);
                            float velocityY = BitConverter.ToSingle(data, 4);
                            float velocityZ = BitConverter.ToSingle(data, 8);

                            float angularX = BitConverter.ToSingle(data, 12);
                            float angularY = BitConverter.ToSingle(data, 16);
                            float angularZ = BitConverter.ToSingle(data, 20);

                            float rotationX = BitConverter.ToSingle(data, 24);
                            float rotationY = BitConverter.ToSingle(data, 28);
                            float rotationZ = BitConverter.ToSingle(data, 32);

                            float adjustedTilt = BitConverter.ToSingle(data, 36);
                            float currentLean = BitConverter.ToSingle(data, 40);

                            // Extract byte flags
                            bool isFlying = data[44] != 0;
                            bool isRunning = data[45] != 0;
                            bool isHit = data[46] != 0;
                            bool weaponFired = data[47] != 0;
                            bool stomped = data[48] != 0;
                            bool landed = data[49] != 0;
                            bool jumped = data[50] != 0;


                            // Forward the values to the app
                            controller.SetInput(0, velocityX);
                            controller.SetInput(1, velocityY);
                            controller.SetInput(2, velocityZ);

                            controller.SetInput(3, angularX);
                            controller.SetInput(4, angularY);
                            controller.SetInput(5, angularZ);

                            controller.SetInput(6, rotationX);
                            controller.SetInput(7, rotationY);
                            controller.SetInput(8, rotationZ);

                            controller.SetInput(9, adjustedTilt);
                            controller.SetInput(10, currentLean);

                            controller.SetInput(11, isFlying ? 1.0f : 0.0f);
                            controller.SetInput(12, isRunning ? 1.0f : 0.0f);
                            controller.SetInput(13, isHit ? 1.0f : 0.0f);
                            controller.SetInput(14, weaponFired ? 1.0f : 0.0f);
                            controller.SetInput(15, stomped ? 1.0f : 0.0f);
                            controller.SetInput(16, landed ? 1.0f : 0.0f);
                            controller.SetInput(17, jumped ? 1.0f : 0.0f);
                        }
						else
						{
							Debug.WriteLine($"Unexpected data size: {data.Length} bytes. Skipping packet.");
						}
					}
					catch (SocketException ex)
					{
						Debug.WriteLine($"Socket error: {ex.Message}");
						break; // Exit loop on socket error
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"Error: {ex.Message}");
					}

					
				}

				udpClient.Close();
			}).Start();
		}

		public void PatchGame()
		{
			//Pass
		}


		/// <summary>
		/// The app will give us these references. We need to save them
		/// </summary>
		public void SetReferences(IProfileManager controller, IMainFormDispatcher dispatcher)
		{
			this.controller = controller;
			this.dispatcher = dispatcher;
		}

		Stream GetStream(string resourceName)
		{
			var assembly = GetType().Assembly;
			var rr = assembly.GetManifestResourceNames();
			string fullResourceName = $"{assembly.GetName().Name}.Resources.{resourceName}";
			return assembly.GetManifestResourceStream(fullResourceName);
		}

        public Type GetConfigBody()
        {
            return null;
        }
    }
}
