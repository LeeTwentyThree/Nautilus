﻿using BepInEx;
using Nautilus.Assets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Handlers;
using UnityEngine;

namespace Nautilus.Examples;

[BepInPlugin("com.snmodding.nautilus.biomehandler", "Nautilus Biome Example Mod", Nautilus.PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.snmodding.nautilus")]
public class BiomeHandlerExample : BaseUnityPlugin
{
    private void Awake()
    {
        // Register the new biome into the game
        var lilyPadsFogSettings = BiomeHandler.CreateBiomeSettings(new Vector3(20, 5, 6), 0.6f, Color.white, 0.45f,
            new Color(0.18f, 0.604f, 0.404f), 0.05f, 20, 1, 1.25f, 20);
#if SUBNAUTICA
        BiomeHandler.RegisterBiome("nautilusexamplebiome", lilyPadsFogSettings, new BiomeHandler.SkyReference("SkyKelpForest"));
#elif BELOWZERO
        BiomeHandler.RegisterBiome("nautilusexamplebiome", lilyPadsFogSettings, new BiomeHandler.SkyReference("SkyLilyPads"));
#endif

        // Create an atmosphere volume for the biome
        PrefabInfo volumePrefabInfo = PrefabInfo.WithTechType("NautilusExampleBiomeSphereVolume");
        CustomPrefab volumePrefab = new CustomPrefab(volumePrefabInfo);
        AtmosphereVolumeTemplate volumeTemplate = new AtmosphereVolumeTemplate(volumePrefabInfo, AtmosphereVolumeTemplate.VolumeShape.Sphere, "nautilusexamplebiome");
        volumePrefab.SetGameObject(volumeTemplate);
        volumePrefab.Register();
        
        // Add the biome somewhere to the world
        CoordinatedSpawnsHandler.RegisterCoordinatedSpawn(new SpawnInfo(volumePrefabInfo.ClassID, new Vector3(-1400, -30, 600), Quaternion.identity));
    }
}