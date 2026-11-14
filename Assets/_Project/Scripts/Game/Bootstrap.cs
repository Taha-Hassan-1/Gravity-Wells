using UnityEngine;

namespace GravityWells.Game
{
    // The single component in the single scene. Everything else is built from here in code.
    public sealed class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60;
        }
    }
}
