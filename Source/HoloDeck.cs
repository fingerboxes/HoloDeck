/*
 * The MIT License (MIT)
 *
 * Copyright (c) 2015 Alexander Taylor
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */﻿

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using UnityEngine;

using HoloDeck.SceneModules;

namespace HoloDeck
{
    // We want to load in all relevant game scenes, and be applied to all games.
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.SPACECENTER, GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.TRACKSTATION })]
    class HoloDeck : ScenarioModule
    {
        // Tag for marking debug logs with
        private const string TAG = "HoloDeck.HoloDeck";

        // This class is a singleton, as much as Unity will allow. 
        // There is probably a better way to do this in a Unity-like way,
        // But I don't know it. 
        public static HoloDeck instance;

        // This is a flag for marking a save as 'dirty'. Any flag with this flag 
        // that enters SPACECENTER, EDITOR, or TRACKSTATION will be immediately reset
        [KSPField(isPersistant = true)]
        public bool SimulationActive = false;

        // This is our entry point
        void Start()
        {            
            // update the singleton
            instance = this;

            // Reload to pre-sim if we are in the wrong scene.
            if (HighLogic.LoadedScene != GameScenes.FLIGHT)
            {
                HoloDeck.Deactivate(GameScenes.SPACECENTER);
            }
            
            // Deploy scene-specific modules, for GUI hijacking and similar logic
            switch (HighLogic.LoadedScene)
            {
                case GameScenes.FLIGHT:
                    gameObject.AddComponent<FlightModule>();
                    break;
                case GameScenes.EDITOR:
                    gameObject.AddComponent<EditorModule>();
                    break;
            }

            // If the sim is active, display the tell-tale
            SimulationNotification(SimulationActive);
        }

        // This is called when the script is destroyed.
        // This is honestly probably not necessary, but better safe than sorry
        void Destroy()
        {
            SimulationNotification(false); 
            instance = null;
        }

        // Activates the Simulation. Returns the success of the activation.
        public static bool Activate()
        {
            // for recording save status, not sure what this string actually is tbh
            string save = null;

            // Make sure the instance actually exists. I can't imagine this ever failing, but NREs are bad.
            if (instance != null)
            {   
                // We create the pre-sim save.
                save = GamePersistence.SaveGame("HoloDeckRevert", HighLogic.SaveFolder, SaveMode.OVERWRITE);

                // Mark the existing save as dirty.
                HoloDeck.instance.SimulationActive = true;

                // Record the scene we are coming from
                HoloDeckShelter.lastScene = HighLogic.LoadedScene;

                if (HoloDeckShelter.lastScene == GameScenes.EDITOR)
                {
                    HoloDeck.OnLeavingEditor(EditorDriver.editorFacility, EditorLogic.fetch.launchSiteName);
                }

                // Start the tell-tale
                HoloDeck.instance.SimulationNotification(true);
            }

            return save != null ? true : false;
        }

        // Deactivates the simulation. Success is destructive to the plugin state,
        // so... no return value
        public static void  Deactivate(GameScenes targetScene)
        {
            // This method only does something if the sim is active.
            if (HoloDeck.instance.SimulationActive)
            {
                // Weird bug can be intorduced by how KSP keeps around KSPAddons until it decides to destroy
                // them. We need to preempt this so extraneous behavior isn't observed
                FlightModule[] flightModules = GameObject.FindObjectsOfType(typeof(FlightModule)) as FlightModule[];
                EditorModule[] editorModules = GameObject.FindObjectsOfType(typeof(EditorModule)) as EditorModule[];

                foreach (FlightModule flightModule in flightModules)
                {
                    DestroyImmediate(flightModule);
                }

                foreach (EditorModule editorModule in editorModules)
                {
                    DestroyImmediate(editorModule);
                }

                // Ok, here is where this is tricky. We can't just directly load the save, we need to 
                // load the save into a new Game object, re-save that object into the default persistence,
                // and then change the scene. Weird shit, right? This is actually how the vanilla quickload
                // works!
                Game newGame = GamePersistence.LoadGame("HoloDeckRevert", HighLogic.SaveFolder, true, false);
                GamePersistence.SaveGame(newGame, "persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                newGame.startScene = targetScene;

                // This has to be before... newGame.Start()
                if (targetScene == GameScenes.EDITOR)
                {
                    newGame.editorFacility = HoloDeckShelter.lastEditor;
                }

                newGame.Start();
                HoloDeck.instance.SimulationActive = false;

                // ... And this has to be after. <3 KSP
                if (targetScene == GameScenes.EDITOR)
                {
                    EditorDriver.StartupBehaviour = EditorDriver.StartupBehaviours.LOAD_FROM_CACHE;
                    ShipConstruction.ShipConfig = HoloDeckShelter.lastShip;
                }
            }
        }

        // This method should be called before activating the simulation directly from an editor, and allows 
        // QOL improvements (like returning to that editor correctly, and automatically clearing the launch site)
        // Either of these values can be null, if you want to do that for some reason
        public static void OnLeavingEditor(EditorFacility facility, string launchSiteName) 
        {
            // clear the launchpad.
            if (launchSiteName != null)
            {
                List<ProtoVessel> junkAtLaunchSite = ShipConstruction.FindVesselsLandedAt(HighLogic.CurrentGame.flightState, launchSiteName);

                foreach (ProtoVessel pv in junkAtLaunchSite)
                {
                    HighLogic.CurrentGame.flightState.protoVessels.Remove(pv);
                }
            }

            if (facility != EditorFacility.None)
            {
                HoloDeckShelter.lastEditor = facility;
            }

            HoloDeckShelter.lastShip = ShipConstruction.ShipConfig;
        }

        // This is in here instead of GUI, because this isn't an 'implementation detail'
        // If some other mod uses sim mode for some reason, I still want this displayed.
        private void SimulationNotification(bool state)
        {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                switch (state)
                {
                    case true:
                        InvokeRepeating("DoSimulationNotification", 0.1f, 1.0f);
                        break;

                    case false:
                        CancelInvoke("DoSimulationNotification");
                        break;
                }
            }
        }

        private void DoSimulationNotification()
        {
            ScreenMessages.PostScreenMessage("Simulation Active", 1.0f, ScreenMessageStyle.LOWER_CENTER);
        }
    }
}
