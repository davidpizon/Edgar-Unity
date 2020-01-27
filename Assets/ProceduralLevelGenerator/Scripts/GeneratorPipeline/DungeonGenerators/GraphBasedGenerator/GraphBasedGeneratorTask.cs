﻿using System.IO;
using System.Linq;
using Assets.ProceduralLevelGenerator.Scripts.Data.Graphs;
using Assets.ProceduralLevelGenerator.Scripts.GeneratorPipeline.RoomTemplates;
using MapGeneration.Core.LayoutGenerators.DungeonGenerator;
using MapGeneration.Interfaces.Core.MapDescriptions;
using Newtonsoft.Json;

namespace Assets.ProceduralLevelGenerator.Scripts.GeneratorPipeline.DungeonGenerators.GraphBasedGenerator
{
	using System;
    using System.Diagnostics;
    using Payloads.Interfaces;
    using Utils;
	using Debug = UnityEngine.Debug;

    /// <summary>
	/// Actual implementation of the task that generates dungeons.
	/// </summary>
	/// <typeparam name="TPayload"></typeparam>
	public class GraphBasedGeneratorTask<TPayload> : GraphBasedGeneratorBaseTask<TPayload, GraphBasedGeneratorConfig>
		where TPayload : class, IGeneratorPayload, IGraphBasedGeneratorPayload, IRandomGeneratorPayload
    {
		private readonly DungeonGeneratorUtils dungeonGeneratorUtils = new DungeonGeneratorUtils();

		public override void Process()
		{
			if (Config.Timeout <= 0)
			{
				throw new ArgumentException("Timeout must be a positive number.");
			}

			var stopwatch = new Stopwatch();
			stopwatch.Start();

			if (Config.ShowDebugInfo)
			{
				Debug.Log("--- Generator started ---"); 
			}

			// Setup map description
			var mapDescription = Payload.LevelDescription.GetMapDescription();

			// TODO: handle exporting map descriptions properly
   //         var json = JsonConvert.SerializeObject(mapDescription, Formatting.Indented, new JsonSerializerSettings()
   //         {
			//	PreserveReferencesHandling = PreserveReferencesHandling.All,
			//	TypeNameHandling = TypeNameHandling.All,
   //         });
			//File.WriteAllText("mapDescription.json", json);
			
			// Generate layout
            var generator = GetGenerator(mapDescription);

            if (Config.UsePrecomputedLevelsOnly)
            {
                if (Config.PrecomputedLevelsHandler == null)
                {
					throw new InvalidOperationException($"{nameof(Config.PrecomputedLevelsHandler)} must not be null when {nameof(Config.UsePrecomputedLevelsOnly)} is enabled");
                }

                Config.PrecomputedLevelsHandler.LoadLevel(Payload);
            }
            else
            {
                var layout = GenerateLayout(mapDescription, generator, Config.Timeout, Config.ShowDebugInfo);
                Payload.GeneratedLayout = layout;
            }

			// TODO: How to handle timeout when benchmarking?
            if (Payload is IBenchmarkInfoPayload benchmarkInfoPayload)
            {
                benchmarkInfoPayload.TimeTotal = generator.TimeTotal;
                benchmarkInfoPayload.Iterations = generator.IterationsCount;
            }

            // Setup room templates
			Payload.GeneratedLevel = TransformLayout(Payload.GeneratedLayout, Payload.LevelDescription);

			// Apply tempaltes
			if (Config.ApplyTemplate)
			{
				ApplyTemplates();
			}

			// Center grid
			if (Config.CenterGrid)
			{
				Payload.Tilemaps[0].CompressBounds();
				Payload.Tilemaps[0].transform.parent.position = -Payload.Tilemaps[0].cellBounds.center;
			}
			
			if (Config.ShowDebugInfo)
			{
				Debug.Log($"--- Completed. {stopwatch.ElapsedMilliseconds / 1000f:F} s ---");
			}
		}

		protected DungeonGenerator<Room> GetGenerator(IMapDescription<Room> mapDescription)
		{
			var generator = new DungeonGenerator<Room>(mapDescription, new DungeonGeneratorConfiguration<Room>(mapDescription) { RoomsCanTouch = false });
			generator.InjectRandomGenerator(Payload.Random);

            return generator;
		}

		/// <summary>
		/// Copies tiles from individual room templates to the tilemaps that hold generated dungeons.
		/// </summary>
		protected void ApplyTemplates()
		{
			var nonCorridors = Payload.GeneratedLevel.GetAllRoomInstances().Where(x => !x.IsCorridor).ToList();
			var corridors = Payload.GeneratedLevel.GetAllRoomInstances().Where(x => x.IsCorridor).ToList();

            dungeonGeneratorUtils.ApplyTemplates(nonCorridors, Payload.Tilemaps);
			dungeonGeneratorUtils.ApplyTemplates(corridors, Payload.Tilemaps);
		}
	}
}