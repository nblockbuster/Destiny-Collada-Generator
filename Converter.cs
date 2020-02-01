using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

class Converter
{
	public static JObject loadTGXBin(byte[] data) 
	{
		Console.WriteLine("Loading model data...");
		
		Console.Write("Reading TGXM header... ");
		string magic = TGXMUtils.String(data, 0x0, 0x4); // TGXM
		int version = (int)BitConverter.ToUInt32(data, 0x4);//TGXMUtils.Uint(data, 0x4);
		int fileOffset = (int)BitConverter.ToUInt32(data, 0x8);//TGXMUtils.Uint(data, 0x8);
		int fileCount = (int)BitConverter.ToUInt32(data, 0xC);//TGXMUtils.Uint(data, 0xC);
		string fileIdentifier = TGXMUtils.String(data, 0x10, 0x100);
		//if (magic != "TGXM") {
		//    console.error('Invalid TGX File', url);
		//    return;
		//}
		Console.WriteLine("Done.");

		dynamic files = new JArray();
		dynamic fileLookup = new JArray();
		dynamic renderMetadata = new JObject();
		for (var f=0; f<fileCount; f++) 
		{
			int headerOffset = fileOffset+(0x110*f);
			string name = TGXMUtils.String(data, headerOffset, 0x100);
			int offset = (int)BitConverter.ToUInt32(data, headerOffset+0x100);//TGXMUtils.Uint(data, headerOffset+0x100);
			int type = (int)BitConverter.ToUInt32(data, headerOffset+0x104);//TGXMUtils.Uint(data, headerOffset+0x104);
			int size = (int)BitConverter.ToUInt32(data, headerOffset+0x108);//TGXMUtils.Uint(data, headerOffset+0x108);
			Console.WriteLine("Loading file \""+name+".\" File size: "+size+" bytes.");
			//byte[] fileData = Arrays.copyOfRange(data, offset, offset+size);
			byte[] fileData = new byte[size];
			Array.ConstrainedCopy(data, offset, fileData, 0, size);

			dynamic file = new JObject();
			file.name = name;
			file.offset = offset;
			file.type = type;
			file.size = size;

			//JSONParser jsonParser = new JSONParser();
			if (name.IndexOf(".js") != -1) 
			{ // render_metadata.js
				//try 
				//{
					//renderMetadata = (JSONObject) jsonParser.parse(TGXMUtils.string(fileData,0,0));
					renderMetadata = JObject.Parse(TGXMUtils.String(fileData,0,0));
					//file.put("data", renderMetadata);
					file.data = renderMetadata;
				//} catch (ParseException e)
				//{}
			} 
			else
			{
				//file.put("data", fileData);
				file.data = fileData;
			}

			files.Add(file);
			fileLookup.Add(name);
			Console.WriteLine("File \""+name+"\" loaded.");
		}

		dynamic tgxBin = new JObject();
		tgxBin.fileIdentifier = fileIdentifier;
		tgxBin.files = files;
		tgxBin.lookup = fileLookup;
		tgxBin.metadata = renderMetadata;

		Console.WriteLine("Done loading model data.");
		return tgxBin;
	}

	public static void Convert(byte[] data, string fileOut) 
	{	
		JObject tgxBin = loadTGXBin(data);
		JArray renderMeshes = Parsers.parseTGXAsset(tgxBin);
		JObject renderModel = new JObject();
		renderModel.meshes = renderMeshes;
		renderModel.textures = null;
		renderModel.name = "Model";
		JArray renderModels = new JArray();
		renderModels.Add(renderModel);

		//using (StreamWriter output = new StreamWriter(@"Output\format2.json"))
		//{
		//    output.Write(renderMeshes.ToString());
		//}

		WriteCollada.WriteFile(renderMeshes, fileOut);
	}

	public static void Convert(byte[][][][] binItems, string fileOut) 
	{	
		JArray renderModels = new JArray();
		foreach (byte[][][] itemContainers in binItems)
		{
			byte[][] geometry = itemContainers[0];
			byte[][] textures = itemContainers[1];
			string name = Encoding.ASCII.GetString(itemContainers[2][0]);
			
			dynamic renderModel = new JObject();
			JArray renderMeshes = new JArray();
			JObject renderTextures = new JObject();
			JArray plates = new JArray();
			
			foreach (byte[] data in geometry)
			{
				JObject tgxBin = loadTGXBin(data);
				JArray meshes = Parsers.parseTGXAsset(tgxBin);
				for (JObject mesh in meshes)
				{
					renderMeshes.Add(mesh);
				}
				plates.Add(tgxBin.metadata.texture_plates);
			}
			
			renderTextures.Item["texturePlates"] = plates;
			
			foreach (byte[] data in textures)
			{
				JObject tgxBin = loadTGXBin(data);
				for (JObject texture in tgxBin.files)
				{
					renderTextures.Item[texture.name] = texture.data;
				}
			}
			
			renderModel.meshes = renderMeshes;
			renderModel.textures = renderTextures;
			renderModel.name = name;
			
			renderModels.Add(renderModel);
		}
		
		//using (StreamWriter output = new StreamWriter(@"Output\format2.json"))
		//{
		//    output.Write(renderMeshes.ToString());
		//}

		WriteCollada.WriteFile(renderModels, fileOut);
	}
}