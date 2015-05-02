using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HoloDeck.SceneModules
{
    class EditorModule : MonoBehaviour
    {
        private const string TAG = "HoloDeck.EditorModule";

        // Grab the KSP base skin, for future reference
        GUISkin Skin = HighLogic.Skin;

        // We need to check if this is being displayed, later
        PopupDialog LaunchDialog = null;

        void Start()
        {
            // Hijack launch button
            EditorLogic.fetch.launchBtn.scriptWithMethodToInvoke = this;
            EditorLogic.fetch.launchBtn.methodToInvoke = "LaunchButton";
        }

        void Destroy()
        {
        }

        void Update()
        {
            // If the LaunchDialog isn't being displayed, but the Controls are locked
            // The reason we are doing this instead of just releasing the locks directly is so that the user doesn't
            // 'click-through' to any parts underneath the popup window
            if (LaunchDialog == null && InputLockManager.GetControlLock("KCT_EDITOR_LAUNCH_B") != ControlTypes.None)
            {
                // Release the locks
                ReleaseControlLock();
            }
        }

        void LaunchButton()
        {
            List<DialogOption> LaunchOptionsList = new List<DialogOption>();
            
            LaunchOptionsList.Add(new DialogOption("Proceed to Launch", InvokeLaunch));
            LaunchOptionsList.Add( new DialogOption("Simulate this Vessel", InvokeSimulation));
            LaunchOptionsList.Add(new DialogOption("Cancel Launch", null));

            // This is how you hide tooltips.
            EditorTooltip.Instance.HideToolTip();
            GameEvents.onTooltipDestroyRequested.Fire();

            // Lock inputs
            EditorLogic.fetch.Lock(true, true, true, "KCT_EDITOR_LAUNCH_A");
            InputLockManager.SetControlLock(ControlTypes.EDITOR_SOFT_LOCK, "KCT_EDITOR_LAUNCH_B");

            // Display the new launch prompt
            LaunchDialog = PopupDialog.SpawnPopupDialog(new MultiOptionDialog(null, null, Skin, LaunchOptionsList.ToArray()), false, Skin);
        }

        void InvokeLaunch()
        {
            ReleaseControlLock();
            EditorLogic.fetch.launchVessel();
        }

        void InvokeSimulation()
        {
            ReleaseControlLock();

            HoloDeck.Activate();                       

            EditorLogic.fetch.launchVessel();
        }    

        void InvokeBuild()
        {

        }

        void ReleaseControlLock()
        {            
            EditorLogic.fetch.Unlock("KCT_EDITOR_LAUNCH_A");
            InputLockManager.RemoveControlLock("KCT_EDITOR_LAUNCH_B");
        }
    }
}
