using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using System.Windows.Forms;
namespace ValheimPerformanceOptimizations.Patches
{
	/// <summary>
	/// Generating the minimap takes a lot of time at startup and is generated from WorldGenerator. Therefore the
	/// minimap never changes in a given world.
	/// Now it is being saved inside the world folder as a zipped file with the name worldName_worldSeed_gameVersion.map
	/// </summary>
	/// 

	[HarmonyPatch(typeof(Minimap), "GenerateWorldMap")]
	public static class MinimapGenerationPatch
	{
		private static bool Prefix(Minimap __instance)
		{
			FileLog.Log("Prefix is called");
			// try to load existing textures
			if (File.Exists(MinimapTextureFilePath()))
			{
				LoadFromFile(__instance);
				return false;
			}

			// compute textures normally
			return true;
		}

		private static void Postfix(Minimap __instance)
		{
			FileLog.Log("Postfix is called");

			if (!File.Exists(MinimapTextureFilePath()))
			{
				// write computed textures to file
				Directory.CreateDirectory(MinimapTextureFolderPath());
				SaveToFile(__instance);
			}
		}

		private static void SaveToFile(Minimap minimap)
		{
			MessageBox.Show("TEST!");

			using var fileStream = File.Create(MinimapTextureFilePath());
			using var compressionStream = new GZipStream(fileStream, System.IO.Compression.CompressionLevel.Fastest);

			var package = new ZPackage();
			var forestMaskTexture = Traverse.Create(minimap)
								  .Field("m_forestMaskTexture")
								  .GetValue<Texture2D>().GetRawTextureData();
			var mapTexture = Traverse.Create(minimap)
							  .Field("m_forestMaskTexture")
							  .GetValue<Texture2D>().GetRawTextureData();
			var heightTexture = Traverse.Create(minimap)
							  .Field("m_forestMaskTexture")
							  .GetValue<Texture2D>().GetRawTextureData();
			package.Write(forestMaskTexture);
			package.Write(mapTexture);
			package.Write(heightTexture);

			var data = package.GetArray();
			compressionStream.Write(data, 0, data.Length);
		}

		private static void LoadFromFile(Minimap minimap)
		{
			FileLog.Log("LoadFromFile is called");
			using var fileStream = File.OpenRead(MinimapTextureFilePath());
			using var decompressionStream = new GZipStream(fileStream, CompressionMode.Decompress);
			using var resultStream = new MemoryStream();

			decompressionStream.CopyTo(resultStream);
			var package = new ZPackage(resultStream.ToArray());
			var mmap = Traverse.Create(minimap);
			mmap.Field("m_forestMaskTexture").GetValue<Texture2D>().LoadRawTextureData(package.ReadByteArray());
			mmap.Field("m_forestMaskTexture").Method("Apply");
			mmap.Field("m_mapTexture").GetValue<Texture2D>().LoadRawTextureData(package.ReadByteArray());
			mmap.Field("m_mapTexture").Method("Apply");
			mmap.Field("m_heightTexture").GetValue<Texture2D>().LoadRawTextureData(package.ReadByteArray());
			mmap.Field("m_heightTexture").Method("Apply");
		}

		public static string MinimapTextureFilePath()
		{
			FileLog.Log("MinimapTextureFilePath is called");
			// for some reason Weyland adds a forward slash to the version string instead of literally anything else
			Version ver = new Version();
			ZNet zNet = new ZNet();
			var versionString = Traverse.Create(ver)
										 .Method("GetVersionString")
										 .GetValue<string>();
			var cleanedVersionString = versionString.Replace("/", "_");
			var world = Traverse.Create(zNet)
								  .Field("m_world").GetValue<World>();
			var file = $"{world.m_name}_{world.m_seed}_{cleanedVersionString}.map";

			return MinimapTextureFolderPath() + "/" + file;
		}

		public static string MinimapTextureFolderPath()
		{
			return World.GetWorldSavePath() + "/minimap";
		}
	}
}
