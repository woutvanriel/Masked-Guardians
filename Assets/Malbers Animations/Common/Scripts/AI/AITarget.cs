﻿using MalbersAnimations.Events;
using UnityEngine;

namespace MalbersAnimations
{
    [HelpURL("https://malbersanimations.gitbook.io/animal-controller/main-components/ai/ai-target")]
    /// <summary>
    /// This Script allows to set a gameObject as a Target for the Ai Logic. 
    /// So when an AI Animal sets a gameObject holding this script  as a target, 
    /// it can have a stopping distance and it can stop on a properly distance.
    /// </summary>
    [AddComponentMenu("Malbers/AI/AI Target")]
    [SelectionBase]
    public class AITarget : MonoBehaviour, IAITarget
    {
        public WayPointType pointType = WayPointType.Ground;
       
        [Tooltip("Distance for AI driven animals to stop when arriving to this gameobject. When is set as the AI Target.")]
        [Min(0)] public float stoppingDistance = 1f;
        
        [Tooltip("Distance for AI driven animals to start slowing its speed when arriving to this gameobject. If its set to zero or lesser than the Stopping distance, the Slowing Movement Logic will be ignored")]
        [Min(0)] public float slowingDistance = 0;
        
        [Tooltip("Offset to correct the Position of the Target")]
        [SerializeField] private Vector3 center;
        
        [Tooltip("Default Height for the Waypoints")]
        [SerializeField] private float m_height = 0.5f;

        [Tooltip(" When the AI animal arrives to the target, do we Rotate the Animal so it looks at the center of the AI Target?")]
        [SerializeField] private bool m_arriveLookAt = true;
        
        public bool ArriveLookAt => m_arriveLookAt;
        public float Height => m_height * transform.localScale.y;

        public WayPointType TargetType => pointType;

        [Space]
        public GameObjectEvent OnTargetArrived = new();

        /// <summary>Center of the Animal to be used for AI and Targeting  </summary>
        public Vector3 Center
        {
            private set => center = value;
            get => transform.TransformPoint(center);
        }

        public void TargetArrived(GameObject target) => OnTargetArrived.Invoke(target);


        public void SetLocalCenter(Vector3 localCenter) => center = localCenter;

        /// <summary>Get the center of the AI Target</summary>
        public virtual Vector3 GetCenterPosition() => Center;

        /// <summary>Get the center of the AI Target plus the Height value</summary>
        public virtual Vector3 GetCenterY() => Center + (transform.up * Height);

        public float StopDistance() => stoppingDistance * transform.localScale.y; //IMPORTANT For Scaled objects like the ball
        public float SlowDistance() => slowingDistance * transform.localScale.y; //IMPORTANT For Scaled objects like the ball

        public void SetGrounded() => pointType = WayPointType.Ground;
        public void SetAir() => pointType = WayPointType.Air;
        public void SetWater() => pointType = WayPointType.Water;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            StopDistanceGizmo(Color.red);
        }

        private void OnDrawGizmosSelected()
        {
            StopDistanceGizmo(Color.yellow);

            if (stoppingDistance < slowingDistance)
            {
                UnityEditor.Handles.color = Color.cyan;
                UnityEditor.Handles.DrawWireDisc(Center, transform.up, slowingDistance * transform.localScale.y);
            }
        }

        private void StopDistanceGizmo(Color color)
        {
            UnityEditor.Handles.color = Gizmos.color = color;
            UnityEditor.Handles.DrawWireDisc(Center, transform.up, stoppingDistance * transform.localScale.y);
            UnityEditor.Handles.DrawWireDisc(Center, transform.up, stoppingDistance * transform.localScale.y * 0.1f);
            Gizmos.DrawRay(Center, transform.up * Height);
            Gizmos.DrawWireSphere(Center + transform.up * Height, stoppingDistance * 0.1f);
        }
#endif
    }
}