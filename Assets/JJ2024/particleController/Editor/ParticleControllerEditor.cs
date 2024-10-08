#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UCParticleControl.ParticleController))]
public class ParticleControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        
        DrawDefaultInspector();
        
        UCParticleControl.ParticleController controller = (UCParticleControl.ParticleController)target;
        
        if (GUILayout.Button("应用粒子设置（每次编辑后需点击）"))
        {
            controller.InitializeParticle();
        }
    }
}
#endif