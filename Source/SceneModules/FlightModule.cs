using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using UnityEngine;

namespace HoloDeck.SceneModules
{
    /*
     * This class contains all of the code for manipulating the GUI, and interacting with the game
     * for HoloDeck in GameScenes.FLIGHT.
     */
 
    class FlightModule : MonoBehaviour
    {
        private const string TAG = "HoloDeck.FlightModule";

        private SimulationPauseMenu menu;

        // Entry Point
        void Start()
        {
           menu = new SimulationPauseMenu();
        }

        /*
         * The basic idea here is that if we are Simming, destroy the vanilla pause menu as soon 
         * as it appears, and replace it with out own. 
         * 
         * If the pause menu is open, close it.
         * 
         * If the pause key is being pressed, check if our menu is active.
         * if our menu is not active, make it active.
         * if our menu is not active, tell it to reduce by one ui layer.
         */         
        void Update()
        {
            // We don't want to do anything if we aren't simming
            if (HoloDeck.instance.SimulationActive)
            {
                // Hide the vanilla pause menu.
                if (PauseMenu.isOpen)
                {
                    PauseMenu.Close();
                }

                // Check for pause keypress
                if (GameSettings.PAUSE.GetKeyDown())
                {
                    switch (menu.isOpen)
                    {
                        case false:
                            menu.Display();
                            break;
                        case true:
                            menu.Close();
                            break;
                    }
                }
            }
        }

        /*
         * When this module is destroyed, make sure to clean up our menu. 
         * It probably won't leak, but I'd rather not tempt fate.
         */
        void Destroy()
        {
            menu.Destroy();
        }
    }

    /*
     * This menu is very similar to the squad PauseMenu.
     * 
     * isOpen - returns true if this menu is in an active state
     * 
     * Display() - makes this menu active
     * Close() - closes one ui layer. 
     */
    class SimulationPauseMenu
    {
        private const float SPACER = 5.0f;
        private const string TAG = "HoloDeck.FlightModule.SimulationPauseMenu";

        private Rect _windowRect;
        private bool _display;
        private Color _backgroundColor;
        private GUISkin _guiSkin;
        private PopupDialog _activePopup;
        private MiniSettings _miniSettings;

        public bool isOpen;

        public SimulationPauseMenu()
        {
            PauseMenu originalMenu = GameObject.FindObjectOfType(typeof(PauseMenu)) as PauseMenu;

            _backgroundColor = originalMenu.backgroundColor;
            _guiSkin = originalMenu.guiSkin;

            RenderingManager.AddToPostDrawQueue(3, new Callback(DrawGUI));
            _windowRect = new Rect((float)(Screen.width / 2.0 - 125.0), (float)(Screen.height / 2.0 - 70.0), 250f, 130f);
        }

        ~SimulationPauseMenu()
        {
            Destroy();
        }

        public void Display()
        {
            isOpen = true;
            _display = true;
            InputLockManager.SetControlLock(ControlTypes.PAUSE, "SimulationPauseMenu");
            FlightDriver.SetPause(true);
        }

        public void Close()
        {
            if (_activePopup != null)
            {
                _activePopup.Dismiss();
                _activePopup = null;
                Unhide();
            }
            else if (_miniSettings != null)
            {
                // WTF SQUAD?
                _miniSettings.GetType().GetMethod("Dismiss", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            else
            {
                isOpen = false;
                _display = false;
                InputLockManager.RemoveControlLock("SimulationPauseMenu");
                FlightDriver.SetPause(false);
            }            
        }

        public void Destroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(3, new Callback(DrawGUI));
        }

        // Screw you, MiniSettings
        private void Hide()
        {
            _display = false;
        }

        private void Unhide()
        {
            _display = true;
        }

        private void DrawGUI()
        {
            if (_display)
            {
                GUI.skin = _guiSkin;
                GUI.backgroundColor = _backgroundColor;
                _windowRect = GUILayout.Window(0, _windowRect, new GUI.WindowFunction(draw), "Game Paused", new GUILayoutOption[0]);
            }
        }

        private void draw(int id)
        {
            if (GUILayout.Button("Resume Simulation", _guiSkin.button))
            {
                Close();
            }

            GUILayout.Space(SPACER);

            if (GUILayout.Button("<color=orange>Abort Simulation</color>", _guiSkin.button))
            {
                _activePopup = PopupDialog.SpawnPopupDialog(new MultiOptionDialog(null, new Callback(drawAbortWarning), "Aborting Simulation", HighLogic.Skin, new DialogOption[0]), false, HighLogic.Skin);
                Hide();
            }

            if (FlightDriver.CanRevertToPostInit)
            {
                if (GUILayout.Button("<color=orange>Restart Simulation</color>", _guiSkin.button))
                {
                    _activePopup = PopupDialog.SpawnPopupDialog(new MultiOptionDialog(null, new Callback(drawRevertWarning), "Reverting Simulation", HighLogic.Skin, new DialogOption[0]), false, HighLogic.Skin);
                    Hide();
                }
            }

            GUILayout.Space(SPACER);

            if (GUILayout.Button("Settings", _guiSkin.button))
            {
                Hide();
                _miniSettings = MiniSettings.Create(Unhide);
            }
        }

        private void drawRevertWarning()
        {
            GUILayout.Label("Reverting will set the game back to an earlier state. Are you sure you want to continue?");
            if (GUILayout.Button("Revert to Launch (" + KSPUtil.PrintTime((int)(Planetarium.GetUniversalTime() - FlightDriver.PostInitState.UniversalTime), 3, false) + " ago)"))
            {
                Close();
                Close();
                FlightDriver.RevertToLaunch();
            }
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
        }

        private void drawAbortWarning()
        {
            string revertTarget;

            GUILayout.Label("Reverting will set the game back to an earlier state. Are you sure you want to continue?");

            switch (HoloDeckShelter.lastScene)
            {
                case GameScenes.EDITOR:
                    switch (HoloDeckShelter.lastEditor)
                    {
                        case EditorFacility.SPH:
                            revertTarget = "Spaceplane Hangar";
                            break;
                        case EditorFacility.VAB:
                            revertTarget = "Vehicle Assembly Building";
                            break;
                        // This should never happen. If it does, just go to the SC
                        default:
                            revertTarget = "Space Center";
                            HoloDeckShelter.lastScene = GameScenes.SPACECENTER;
                            break;
                    }
                    break;
                case GameScenes.SPACECENTER:
                    revertTarget = "Space Center";
                    break;
                default:
                    revertTarget = "Pre-Simulation";
                    break;
            }

            if (GUILayout.Button("Revert to " + revertTarget + " (" + KSPUtil.PrintTime((int)(Planetarium.GetUniversalTime() - FlightDriver.PostInitState.UniversalTime), 3, false) + " ago)"))
            {
                Close();
                Close();
                HoloDeck.Deactivate(HoloDeckShelter.lastScene);
            }
            
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
        }
    }
}
