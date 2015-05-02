using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HoloDeck.SceneModules
{
    // This class is a place to stick data that will not be destroyed by HoloDeck.Deactivate(). 
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    class HoloDeckShelter : MonoBehaviour
    {
        // Editor stuff
        public static EditorFacility lastEditor = EditorFacility.None;
        public static ConfigNode lastShip = null;

        public static GameScenes lastScene;

        void Awake()
        {
            DontDestroyOnLoad(this);
        }
    }
}
