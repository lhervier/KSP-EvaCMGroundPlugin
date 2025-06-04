using System.Collections.Generic;
using UnityEngine;

namespace com.github.lhervier.ksp {
	
	[KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
    public class TestPlugin : MonoBehaviour {
        
        private Part previousPart;
        private Vector3d previousPosition;

        private void Log(string message) {
            Debug.Log("[TestPlugin] " + message);
        }

        public void Start() {
            GameEvents.onEditorPartEvent.Add(OnEditorPartEvent);
            
            
            
            
            GameEvents.onAboutToSaveShip.Add((ShipConstruct ship) => {
                Log($"onAboutToSaveShip: {ship.persistentId}");
            });
            GameEvents.onActiveJointNeedUpdate.Add((Vessel vessel) => {
                Log($"onActiveJointNeedUpdate: {vessel.name}");
            });
            GameEvents.onCollision.Add((EventReport report) => {
                Log($"onCollision: {report.msg}");
            });
            GameEvents.OnCollisionEnhancerHit.Add((Part part, UnityEngine.RaycastHit hitInfo) => {
                Log($"onCollisionEnhancerHit: {part.name} - {hitInfo.point}");
            });
            GameEvents.OnCollisionIgnoreUpdate.Add(() => {
                Log($"onCollisionIgnoreUpdate");
            });
            GameEvents.onEditorCompoundPartLinked.Add((CompoundPart part) => {
                Log($"onEditorCompoundPartLinked: {part.name}");
            });
            GameEvents.onEditorConstructionModeChange.Add((ConstructionMode mode) => {
                Log($"onEditorConstructionModeChange: {mode}");
            });
            GameEvents.onEditorRestoreState.Add(() => {
                Log($"onEditorRestoreState");
            });
            GameEvents.onEditorShipModified.Add((ShipConstruct ship) => {
                Log($"onEditorShipModified: {ship.persistentId}");
            });
            GameEvents.onEditorStarted.Add(() => {
                Log($"onEditorStarted");
            });
            GameEvents.OnEVAConstructionMode.Add((bool mode) => {
                Log($"OnEVAConstructionMode: {mode}");
            });
            GameEvents.OnEVAConstructionModeChanged.Add((ConstructionMode mode) => {
                Log($"OnEVAConstructionModeChanged: {mode}");
            });
            GameEvents.OnEVAConstructionModePartAttached.Add((Vessel vessel, Part part) => {
                Log($"OnEVAConstructionModePartAttached: {vessel.name} - {part.name}");
            });
            GameEvents.OnEVAConstructionModePartDetached.Add((Vessel vessel, Part part) => {
                Log($"OnEVAConstructionModePartDetached: {vessel.name} - {part.name}");
            });
            GameEvents.OnEVAConstructionWeldFinish.Add((KerbalEVA eva) => {
                Log($"OnEVAConstructionWeldFinish: {eva.name}");
            });
            GameEvents.OnEVAConstructionWeldStart.Add((KerbalEVA eva) => {
                Log($"OnEVAConstructionWeldStart: {eva.name}");
            });
            GameEvents.OnEVAConstructionWeldStart.Add((KerbalEVA eva) => {
                Log($"OnEVAConstructionWeldStart: {eva.name}");
            });
            GameEvents.OnExpansionSystemLoaded.Add(() => {
                Log($"OnExpansionSystemLoaded");
            });
            GameEvents.OnFlightCompoundPartDetached.Add((CompoundPart part) => {
                Log($"OnFlightCompoundPartDetached: {part.name}");
            });
            GameEvents.OnFlightCompoundPartLinked.Add((CompoundPart part) => {
                Log($"OnFlightCompoundPartLinked: {part.name}");
            });
            GameEvents.onGameStateLoad.Add((ConfigNode node) => {
                Log($"onGameStateLoad: {node.name}");
            });
            GameEvents.onGameStateSave.Add((ConfigNode node) => {
                Log($"onGameStateSave: {node.name}");
            });
            GameEvents.onGameStateCreated.Add((Game game) => {
                Log($"onGameStateCreated: {game.Title}");
            });
            GameEvents.onGameStateSaved.Add((Game game) => {
                Log($"onGameStateSaved: {game.Title}");
            });
            GameEvents.onGlobalEvaPhysicMaterialChanged.Add((PhysicMaterial material) => {
                Log($"onGlobalEvaPhysicMaterialChanged: {material.name}");
            });
            GameEvents.onKrakensbaneDisengage.Add((Vector3d position) => {
                Log($"onKrakensbaneDisengage: {position}");
            });
            GameEvents.onKrakensbaneEngage.Add((Vector3d position) => {
                Log($"onKrakensbaneEngage: {position}");
            });
            GameEvents.onKrakensbaneDisengage.Add((Vector3d position) => {
                Log($"onKrakensbaneDisengage: {position}");
            });
            GameEvents.onKrakensbaneEngage.Add((Vector3d position) => {
                Log($"onKrakensbaneEngage: {position}");
            });
            GameEvents.onPartActionInitialized.Add((Part part) => {
                Log($"onPartActionInitialized: {part.name}");
            });
            GameEvents.onPartUnpack.Add((Part part) => {
                Log($"onPartUnpack: {part.name}");
            });
            GameEvents.onPartPack.Add((Part part) => {
                Log($"onPartPack: {part.name}");
            });
            GameEvents.onPartUnpack.Add((Part part) => {
                Log($"onPartUnpack: {part.name}");
            });
            GameEvents.onPhysicsEaseStart.Add((Vessel vessel) => {
                Log($"onPhysicsEaseStart: {vessel.name}");
            });
            GameEvents.onPhysicsEaseStop.Add((Vessel vessel) => {
                Log($"onPhysicsEaseStop: {vessel.name}");
            });
            GameEvents.onPhysicsEaseStop.Add((Vessel vessel) => {
                Log($"onPhysicsEaseStop: {vessel.name}");
            });
            GameEvents.onPhysicsEaseStop.Add((Vessel vessel) => {
                Log($"onPhysicsEaseStop: {vessel.name}");
            });
            GameEvents.onProtoPartFailure.Add((ProtoPartSnapshot part) => {
                Log($"onProtoPartFailure: {part.partName}");
            });
            GameEvents.onProtoPartSnapshotLoad.Add((GameEvents.FromToAction<ProtoPartSnapshot, ConfigNode> action) => {
                Log($"onProtoPartSnapshotLoad: {action.from.partName} - {action.to.name}");
            });
            GameEvents.onProtoPartSnapshotSave.Add((GameEvents.FromToAction<ProtoPartSnapshot, ConfigNode> action) => {
                Log($"onProtoPartSnapshotSave: {action.from.partName} - {action.to.name}");
            });
            GameEvents.onProtoVesselSave.Add((GameEvents.FromToAction<ProtoVessel, ConfigNode> action) => {
                Log($"onProtoVesselSave: {action.from.vesselName} - {action.to.name}");
            });
            GameEvents.onProtoVesselLoad.Add((GameEvents.FromToAction<ProtoVessel, ConfigNode> action) => {
                Log($"onProtoVesselLoad: {action.from.vesselName} - {action.to.name}");
            });
            
            Log("Plugin started");
        }

        // ==============================================================================================

        bool IsGrounded(Collider collider) {
            if (collider is BoxCollider boxCollider) {
                return IsGrounded(boxCollider);
            }
            else if (collider is CapsuleCollider capsuleCollider) {
                return IsGrounded(capsuleCollider);
            }
            else if (collider is SphereCollider sphereCollider) {
                return IsGrounded(sphereCollider);
            }
            else {
                Log($"ERROR : Unsupported collider: {collider.name} (position: {collider.transform.position})");
                return false;
            }
        }

        bool IsGrounded(BoxCollider boxCollider) {
            Log($"IsGrounded(BoxCollider): {boxCollider.name}");
            return Physics.CheckBox(
                boxCollider.transform.position,
                boxCollider.size * 0.5f,
                boxCollider.transform.rotation,
                LayerMask.GetMask("TerrainColliders")
            );
        }

        bool IsGrounded(CapsuleCollider capsuleCollider) {
            Log($"IsGrounded(CapsuleCollider): {capsuleCollider.name}");
            Vector3 center = capsuleCollider.transform.position;
            float radius = capsuleCollider.radius;
            float height = capsuleCollider.height;
            Vector3 direction = capsuleCollider.transform.up;
            
            return Physics.CheckCapsule(
                center - direction * (height * 0.5f),
                center + direction * (height * 0.5f),
                radius,
                LayerMask.GetMask("TerrainColliders")
            );
        }

        bool IsGrounded(SphereCollider sphereCollider) {
            Log($"IsGrounded(SphereCollider): {sphereCollider.name}");
            return Physics.CheckSphere(
                sphereCollider.transform.position,
                sphereCollider.radius,
                LayerMask.GetMask("TerrainColliders")
            );
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part) {
            Log($"onEditorPartEvent: {eventType} - {part.name}:{part.persistentId}");

            if( part != this.previousPart ) {
                this.previousPart = part;
                this.previousPosition = part.transform.position;
                Log($"  - New part. Using current position as previous position: {this.previousPosition}");
            }
            else {
                Log($"  - Same part as previous. Using stored previous position: {this.previousPosition}");
            }   
            
            Collider[] colliders = part.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders) {
                Log($"Collider: {collider.name}");
                if (IsGrounded(collider)) {
                    Log($"=> Grounded ! Restoring previous position: {this.previousPosition}");
                    part.transform.position = this.previousPosition;    
                    break;
                }
                else {
                    Log($"=> Not grounded...");
                }
            }
            this.previousPosition = part.transform.position;
        }
    }
}
