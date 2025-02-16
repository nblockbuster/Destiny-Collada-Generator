using System;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace DestinyColladaGenerator
{
	// Methods for accessing the bungie.net web api
	class ApiSupport
	{
		private static string apiKey = PrivateData.apiKey;
		private static string apiRoot = @"https://www.bungie.net/Platform";
		private static string d1ApiRoot = @"https://www.bungie.net/d1/Platform";

		private static Dictionary<string, DestinyInventoryItemDefinition> _inventoryItems;
		private static SqliteConnection _gearAssetConnection = new SqliteConnection("Data Source=" + Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localGearAssetDatabase.db" }));
		private static SqliteConnection _d1ContentConnection = new SqliteConnection("Data Source=" + Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localD1ContentDatabase.db" }));
		private static SqliteConnection _d1GearAssetConnection = new SqliteConnection("Data Source=" + Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localD1GearAssetDatabase.db" }));

		/// <summary>
		/// Loads the local inventory items from the json file
		/// </summary>
		public static void LoadLocalInventoryItems(string game)
		{
			string fileName = "localInventoryItem.json";
			string inventoryItemLiteJson = File.ReadAllText(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", fileName }));
			_inventoryItems = JsonSerializer.Deserialize<Dictionary<string, DestinyInventoryItemDefinition>>(inventoryItemLiteJson);
		}

		/// <summary>
		/// Loads the local gear assets from the sqlite database
		/// </summary>
		public static void OpenGearAssetConnection()
		{
			_gearAssetConnection.Open();
		}

		public static void OpenD1ContentConnection()
		{
			_d1ContentConnection.Open();
		}

		public static void OpenD1GearAssetConnection()
		{
			_d1GearAssetConnection.Open();
		}

		/// <summary>
		/// Makes an HTTP request to the given url, with the X-API-Key header
		/// </summary>
		/// <param name="url">URL of the site to request</param>
		/// <returns>A string of the requested URL</returns>
		public static string MakeCallJson(string url)
		{
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

				var response = client.GetAsync(url).Result;
				string content = response.Content.ReadAsStringAsync().Result;
				if (content.StartsWith('<') == true) return null;
				return content;
			}
		}

		/// <summary>
		/// Looks up the InventoryItem definition in the local json file
		/// </summary>
		/// <param name="game">Defines which game the model is from. Empty string for D1, "2" for D2</param>
		/// <param name="itemHash">Hash of the item to retrieve</param>
		/// <returns>A DestinyInventoryItemDefinition object of the requested data.</returns>

		public static DestinyInventoryItemDefinition GetLocalItemDef(string game, string itemHash)
		{
			DestinyInventoryItemDefinition item = null;
			if (game != "2")
			{
				int itemHashInt = unchecked((int)Convert.ToInt64(itemHash, 10));
				string sql = "SELECT json FROM DestinyInventoryItemDefinition WHERE id = " + itemHashInt;
				
				if (_d1ContentConnection.State != System.Data.ConnectionState.Open)
					OpenD1ContentConnection();

				using (var command = new SqliteCommand(sql, _d1ContentConnection))
				{
					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							item = JsonSerializer.Deserialize<DestinyInventoryItemDefinition>(reader.GetString(0));
						}
					}
				}

				return item;
			}
			if (_inventoryItems == null)
			{
				LoadLocalInventoryItems(game);
			}
			if (_inventoryItems.ContainsKey(itemHash))
				item = _inventoryItems[itemHash];
			return item;
		}

        /// <summary>
        /// Looks up the GearAsset definition in the local gear asset SQL database
        /// </summary>
        /// <param name="game">Defines which game the model is from. Empty string for D1, "2" for D2</param>
        /// <param name="itemHash">Hash of the item to retrieve</param>
        /// <returns>A DestinyGearAssetsDefinition object of the requested data</returns>

        public static DestinyGearAssetsDefinition GetLocalGearAssetDef(string game, string itemHash)
		{
			DestinyGearAssetsDefinition gear = null;
			// Convert itemHash to signed int, ignoring overflow (the hashes overflow into the negatives anyway)
			int itemHashInt = unchecked((int)Convert.ToInt64(itemHash, 10));
			string sql = "SELECT json FROM DestinyGearAssetsDefinition WHERE id = " + itemHashInt;
			if (game != "2")
			{
				if (_d1GearAssetConnection.State != System.Data.ConnectionState.Open)
					OpenD1GearAssetConnection();

				using (var command = new SqliteCommand(sql, _d1GearAssetConnection))
				{
					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							gear = JsonSerializer.Deserialize<DestinyGearAssetsDefinition>(reader.GetString(0));
						}
					}
				}

				return gear;
			}
			if (_gearAssetConnection.State != System.Data.ConnectionState.Open)
				OpenGearAssetConnection();

			using (var command = new SqliteCommand(sql, _gearAssetConnection))
			{
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						gear = JsonSerializer.Deserialize<DestinyGearAssetsDefinition>(reader.GetString(0));
					}
				}
			}

			return gear;
		}

        /// <summary>
        /// Makes an HTTP request to the given URL, with the X-API-Key header.
        /// </summary>
        /// <param name="url">URL of the site to request</param>
        /// <param name="game">Defines which game the model is from. Empty string for D1, "2" for D2</param>
        /// <returns>Dynamic object of the requested data</returns>
        public static dynamic MakeCallGear(string url, string game)
		{
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

				var response = client.GetAsync(url).Result;
				var content = response.Content.ReadAsStringAsync().Result;
				dynamic item = null;
				if (game.Equals(""))
					item = JsonSerializer.Deserialize<D1Shader>(content);
				else
					item = JsonSerializer.Deserialize<D2Shader>(content);

				return item;
			}
		}

		/// <summary>
		/// Makes an HTTP request to the given URL, with the X-API-Key header.
		/// </summary>
		/// <param name="url">URL to request</param>
		/// <returns>A byte array of the requested URL</returns>
		public static byte[] MakeCall(string url)
		{
			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

				var response = client.GetAsync(url).Result;
				var content = response.Content.ReadAsByteArrayAsync().Result;

				return content;
			}
		}

		/// <summary>
		/// Updates the locally stored databases of the D2 InventoryItems and GearAssets
		/// </summary>
		public static void UpdateLocalManifest()
		{
			string manifestJson = MakeCallJson(apiRoot + "/Destiny2/Manifest/");
			var jobj = JObject.Parse(manifestJson);
			DestinyManifestDefinition manifest = JsonSerializer.Deserialize<DestinyManifestDefinition>(jobj["Response"].ToString());

			// open Resources/localManifestVersion to check if the manifest is up to date
			if (File.Exists(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localManifestVersion" })))
			{
				string localManifestVersion = File.ReadAllText(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localManifestVersion" }));
				if (localManifestVersion.Equals(manifest.version))
				{
					Console.WriteLine("Manifest is up to date.");
					return;
				}
			}

			Console.WriteLine("Saving gear asset database.");
			string sqlGearAssetPath = manifest.mobileGearAssetDataBases[1]["path"].ToString();
			byte[] compressedSqlBytes = MakeCall("https://www.bungie.net" + sqlGearAssetPath);

			using (var compressedStream = new MemoryStream(compressedSqlBytes))
			{
				var zipStream = new System.IO.Compression.ZipArchive(compressedStream, System.IO.Compression.ZipArchiveMode.Read);
				var fileStream = new FileStream(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localGearAssetDatabase.db" }), FileMode.OpenOrCreate);
				zipStream.Entries[0].Open().CopyTo(fileStream);
				fileStream.Close();
				zipStream.Dispose();
			}
			Console.WriteLine("Done.");
			Console.WriteLine("Saving inventory item json.");

			string inventoryItemLitePath = manifest.jsonWorldComponentContentPaths["en"]["DestinyInventoryItemDefinition"];
			string invJson = MakeCallJson("https://www.bungie.net" + inventoryItemLitePath);

			File.WriteAllText(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localInventoryItem.json" }), invJson);
			Console.WriteLine("Done.");

			File.WriteAllText(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localManifestVersion" }), manifest.version);
		}

		/// <summary>
		/// Downloads the D1 InventoryItems and GearAssets databases
		/// </summary>
		public static void DownloadD1Manifest()
		{
			string manifestJson = MakeCallJson(d1ApiRoot + "/Destiny/Manifest/");
			var jobj = JObject.Parse(manifestJson);
			DestinyManifestDefinition manifest = JsonSerializer.Deserialize<DestinyManifestDefinition>(jobj["Response"].ToString());

			// open Resources/localManifestVersion to check if the manifest is up to date
			if (File.Exists(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localD1GearAssetDatabase.db" })))
			{
				return;
			}

			Console.WriteLine("Saving gear asset database.");
			string sqlGearAssetPath = manifest.mobileGearAssetDataBases[1]["path"].ToString();
			byte[] compressedSqlBytes = MakeCall("https://www.bungie.net" + sqlGearAssetPath);

			using (var compressedStream = new MemoryStream(compressedSqlBytes))
			{
				var zipStream = new System.IO.Compression.ZipArchive(compressedStream, System.IO.Compression.ZipArchiveMode.Read);
				var fileStream = new FileStream(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localD1GearAssetDatabase.db" }), FileMode.OpenOrCreate);
				zipStream.Entries[0].Open().CopyTo(fileStream);
				fileStream.Close();
				zipStream.Dispose();
			}
			
			Console.WriteLine("Done.\nSaving content database.");

			string contentPath = manifest.mobileWorldContentPaths["en"];
			compressedSqlBytes = MakeCall("https://www.bungie.net" + contentPath);

			using (var compressedStream = new MemoryStream(compressedSqlBytes))
			{
				var zipStream = new System.IO.Compression.ZipArchive(compressedStream, System.IO.Compression.ZipArchiveMode.Read);
				var fileStream = new FileStream(Path.Combine(new string[] { AppDomain.CurrentDomain.BaseDirectory, "Resources", "localD1ContentDatabase.db" }), FileMode.OpenOrCreate);
				zipStream.Entries[0].Open().CopyTo(fileStream);
				fileStream.Close();
				zipStream.Dispose();
			}

			Console.WriteLine("Done.");
		}

			/// <summary>
			/// Converts the list of hashes to Collada models
			/// </summary>
			/// <param name="game">Defines which game the model is from. Empty string for D1, "2" for D2</param>
			/// <param name="hashes">List of API hashes to convert</param>
			/// <param name="fileOut">Output directory to export to</param>
			public static void convertByHash(string game, string[] hashes = null, string fileOut = "")
		{
			bool runConverter = true;
			while (runConverter)
			{
				string[] itemHashes = null;
				if (hashes == null)
				{
					Console.Write("Input item hash(es) > ");
					itemHashes = Console.ReadLine().Split(" ", System.StringSplitOptions.RemoveEmptyEntries);
				}
				else itemHashes = hashes;

				bool skipMainConvert = false;
				if (itemHashes.Length > 1 && Program.multipleFolderOutput)
				{
					for (int h = 0; h < itemHashes.Length; h++)
						convertByHash(game, new string[] { itemHashes[h] }, fileOut);
					skipMainConvert = true;
				}

				ShaderPresets.propertyChannels = new Dictionary<uint, Channels>();
				ShaderPresets.propertyChannels.Clear();
				ShaderPresets.presets = new Dictionary<string, string>();
				ShaderPresets.channelData = new Dictionary<Channels, D2MatProps>();
				ShaderPresets.channelData.Clear();
				ShaderPresets.channelTextures = new Dictionary<Channels, D2TexturesContainer>();
				ShaderPresets.channelTextures.Clear();
				ShaderPresets.scripts = new Dictionary<string, string>();

				if (itemHashes.Length > 0 && !skipMainConvert)
				{
					if (hashes == null)
					{
						Console.Write("Output directory > ");
						fileOut = Console.ReadLine();
					}
					if (fileOut == "") fileOut = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
					else fileOut = Path.GetFullPath(fileOut);

					if (!Directory.Exists(fileOut))
					{
						Directory.CreateDirectory(fileOut);
					}

					List<APIItemData> items = new List<APIItemData>();

					List<string> names = new List<string>();
					List<int> counts = new List<int>();

					foreach (string itemHash in itemHashes)
					{
						Console.WriteLine("Calling item definition from manifest... ");
						DestinyInventoryItemDefinition itemDef = null;
						DestinyGearAssetsDefinition gearAsset = null;
						if (game == "2")
						{
							UpdateLocalManifest();
						}
						else
						{
							DownloadD1Manifest();
						}
						itemDef = GetLocalItemDef(game, itemHash);
						gearAsset = GetLocalGearAssetDef(game, itemHash);

						if (itemDef == null) { Console.WriteLine("Item not found. Skipping."); continue; }
						if (gearAsset == null) { Console.WriteLine("Item is not marked as a gearasset. May be classified in this tool's manifest."); continue; }
						Console.WriteLine("Done calling item definition from manifest.");

						APIItemData itemContainers = new APIItemData();

						List<byte[]> geometryContainers = new List<byte[]>();
						List<byte[]> textureContainers = new List<byte[]>();
						string itemName, itemType = "";
						uint itemBucket = 0;
						if (game == "2")
						{
							itemName = itemDef.displayProperties["name"].ToString();
							itemType = itemDef.itemTypeDisplayName;
							itemBucket = itemDef.inventory.bucketTypeHash;
						}
						else
						{
							itemName = itemDef.itemName;
							itemType = itemDef.itemTypeName;
							itemBucket = itemDef.bucketTypeHash;
						}
						itemContainers.type = itemType;
						if (itemType == "Shader")
						{
							Console.WriteLine("Found shader. Skipping.");
							continue;
						}
						itemContainers.bucket = itemBucket;

						if (Program.multipleFolderOutput)
							WriteCollada.multiOutItemName = itemName;

						int nameIndex = names.IndexOf(itemName);
						if (nameIndex == -1)
						{
							names.Add(itemName);
							counts.Add(0);
						}
						else
						{
							counts[nameIndex]++;
							itemName += "-" + counts[nameIndex];
						}

						if (gearAsset.content.Length > 0)
						{
							string[] geometries = gearAsset.content[0].geometry;
							bool tG = geometries != null;

							string[] textures = gearAsset.content[0].textures;
							bool tT = textures != null;

							if (gearAsset.content[0].region_index_sets != null)
							{
								for (int g = 0; g < geometries.Length; g++)
								{
									byte[] geometryContainer = MakeCall($@"https://www.bungie.net/common/destiny{game}_content/geometry/platform/mobile/geometry/{geometries[g]}");
									geometryContainers.Add(geometryContainer);
								}

								for (int t = 0; t < textures.Length; t++)
								{
									byte[] textureContainer = MakeCall($@"https://www.bungie.net/common/destiny{game}_content/geometry/platform/mobile/textures/{textures[t]}");
									textureContainers.Add(textureContainer);
								}

								itemContainers.geometry = geometryContainers.ToArray();
								itemContainers.texture = textureContainers.ToArray();
								itemContainers.name = itemName;
								items.Add(itemContainers);
							}
							else if ((gearAsset.content[0].female_index_set != null) && (gearAsset.content[0].male_index_set != null))
							{
								IndexSet mSet = gearAsset.content[0].male_index_set;
								IndexSet fSet = gearAsset.content[0].female_index_set;

								for (int index = 0; index < mSet.geometry.Length; index++)
								{
									int g = mSet.geometry[index];
									if (fSet.geometry.Contains(g)) continue;
									byte[] geometryContainer = MakeCall($@"https://www.bungie.net/common/destiny{game}_content/geometry/platform/mobile/geometry/{geometries[g]}");
									geometryContainers.Add(geometryContainer);
								}

								for (int t = 0; t < textures.Length; t++)
								{
									byte[] textureContainer = MakeCall($@"https://www.bungie.net/common/destiny{game}_content/geometry/platform/mobile/textures/{textures[t]}");
									textureContainers.Add(textureContainer);
								}

								itemContainers.geometry = geometryContainers.ToArray();
								itemContainers.texture = textureContainers.ToArray();
								itemContainers.name = "Male_" + itemName;
								items.Add(itemContainers);

								APIItemData itemContainersFemale = new APIItemData();
								itemContainersFemale.type = itemType;
								List<byte[]> geometryContainersFemale = new List<byte[]>();
								List<byte[]> textureContainersFemale = new List<byte[]>();

								for (int index = 0; index < fSet.geometry.Length; index++)
								{
									int g = fSet.geometry[index];
									if (mSet.geometry.Contains(g)) continue;
									byte[] geometryContainer = MakeCall($@"https://www.bungie.net/common/destiny{game}_content/geometry/platform/mobile/geometry/{geometries[g]}");
									geometryContainersFemale.Add(geometryContainer);
								}

								for (int t = 0; t < textures.Length; t++)
								{
									byte[] textureContainer = MakeCall($@"https://www.bungie.net/common/destiny{game}_content/geometry/platform/mobile/textures/{textures[t]}");
									textureContainersFemale.Add(textureContainer);
								}

								itemContainersFemale.geometry = geometryContainersFemale.ToArray();
								itemContainersFemale.texture = textureContainersFemale.ToArray();
								itemContainersFemale.name = "Female_" + itemName;
								items.Add(itemContainersFemale);
							}
							else if (tG == false && textures.Length != 0)
							{
								for (int t = 0; t < textures.Length; t++)
								{
									byte[] textureContainer = MakeCall($@"https://www.bungie.net/common/destiny{game}_content/geometry/platform/mobile/textures/{textures[t]}");
									textureContainers.Add(textureContainer);
								}

								itemContainers.geometry = geometryContainers.ToArray();
								itemContainers.texture = textureContainers.ToArray();
								itemContainers.name = itemName;
								items.Add(itemContainers);
							}
							else
							{
								Console.WriteLine(itemName + " has no geometry or textures, or is missing a gendered index set.");
							}
						}
						else Console.WriteLine("Item has no content. Skipping geometry and textures.");
						ShaderPresets.propertyChannels.Clear();
						ShaderPresets.channelData.Clear();
						ShaderPresets.channelTextures.Clear();
						ShaderPresets.generatePresets(game, itemDef, gearAsset, itemName);
					}
					Converter.Convert(items.ToArray(), fileOut, game);
				}
				else if (skipMainConvert)
				{ }
				else
					Console.WriteLine("No hashes given.");

				while (true)
				{
					if (hashes != null) { runConverter = false; break; }
					Console.Write("Convert another file? (Y/N) ");
					string runAgain = Console.ReadLine();

					if (runAgain.ToUpper() == "Y")
					{
						break;
					}
					else if (runAgain.ToUpper() == "N")
					{
						runConverter = false;
						break;
					}
					else
					{
						Console.WriteLine("Invalid input");
					}
				}
			}
		}

		public class contentManifest
		{
			public contentManifestEntry[] entries { get; set; }
		}

		public class contentManifestEntry
		{
			public int id { get; set; }
			public string json { get; set; }
		}

		public static void convertContentManifest(string game)
		{
			Console.Write("Manifest location > ");
			string manifestLocation = Console.ReadLine();
			string manifestContent = File.ReadAllText(manifestLocation);
			dynamic manifestData = JsonSerializer.Deserialize<contentManifest>(manifestContent);

			Console.Write("Starting hash > ");
			int startHash = Convert.ToInt32(Console.ReadLine());

			Console.Write("Output directory > ");
			string fileOut = Console.ReadLine();
			if (fileOut == "") fileOut = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
			else fileOut = Path.GetFullPath(fileOut);

			if (!Directory.Exists(fileOut))
			{
				Directory.CreateDirectory(fileOut);
			}

			string failHashes = "";
			for (int h = startHash; h < manifestData.entries.Length; h++)
			{
				string itemHash = unchecked((uint)manifestData.entries[h].id).ToString();
				try
				{
					convertByHash(game, new string[] { itemHash }, fileOut);
				}
				catch
				{
					failHashes += itemHash;
				}
			}
			File.WriteAllText(Path.Combine(fileOut, "FailedHashes.txt"), failHashes);
		}

		public static void customCall()
		{
			Console.Write("Key > ");
			string userKey = Console.ReadLine();
			if (userKey != "") apiKey = userKey;

			Console.Write("Call > ");
			string callContent = Console.ReadLine();

			Console.Write("Output name > ");
			string fileOut = Console.ReadLine();
			if (fileOut == "") fileOut = "Response.txt";

			byte[] callResponse = MakeCall(callContent);

			using (BinaryWriter output = new BinaryWriter(File.Open(fileOut, FileMode.Create)))
			{
				output.Write(callResponse);
			}

			Console.WriteLine("Done.");
		}
	}
}