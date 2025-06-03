using UnityEngine;

namespace com.github.lhervier.ksp {
	
	[KSPAddon(KSPAddon.Startup.PSystemSpawn, false)]
    public class TestPlugin : MonoBehaviour {
        
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

        private double GetAltitudeAboveGround(Part part) {
            Log("--------------------------------");
            Log($"GetAltitudeAboveGround: {part.name}:{part.persistentId}");
            Log($"- World :");
            Log($"  - Position: {part.transform.position}");
            Log($"  - Rotation (Euler): {part.transform.rotation.eulerAngles}");
            Log($"  - Forward: {part.transform.rotation * Vector3.forward}");
            Log($"  - Up (world): {part.transform.rotation * Vector3.up}");
            Log($"  - Right: {part.transform.rotation * Vector3.right}");
            Log($"- Local:");
            Log($"  - Position: {part.transform.localPosition}");
            Log($"  - Rotation (Euler): {part.transform.localRotation.eulerAngles}");
            Log($"  - Forward: {part.transform.localRotation * Vector3.forward}");
            Log($"  - Up: {part.transform.localRotation * Vector3.up}");
            Log($"  - Right: {part.transform.localRotation * Vector3.right}");
            
            // Obtenir tous les colliders de la pièce
            Collider[] colliders = part.GetComponentsInChildren<Collider>();
            Log($"- Nb colliders: {colliders.Length}");

            double minAltitudeAboveGround = float.MaxValue;

            foreach (Collider collider in colliders) {
                Log($"Collider: {collider.name}");

                // Obtenir les points de collision (les coins du collider)
                // Dans les coordonnées monde
                Vector3[] corners = GetColliderCorners(collider);
                
                foreach (Vector3 corner in corners) {
                    Log($"- Corner (world): {corner}");

                    // Convertir la position en coordonnées géographiques
                    double latitude = FlightGlobals.currentMainBody.GetLatitude(corner);
                    double longitude = FlightGlobals.currentMainBody.GetLongitude(corner);
                    Log($"  - Latitude: {latitude} - Longitude: {longitude}");
                    
                    // Calculer l'altitude du point par rapport au niveau de la mer
                    double cornerAltitude = FlightGlobals.currentMainBody.GetAltitude(corner);
                    Log($"  - Altitude: {cornerAltitude}");
                    
                    // Obtenir la hauteur du terrain à cette position
                    double terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(latitude, longitude, true);
                    Log($"  - Altitude du terrain: {terrainAltitude}");

                    // Si un seul point est sous le terrain, la pièce est considérée comme sous terre
                    double altitudeAboveGround = cornerAltitude - terrainAltitude;
                    Log($"  - Altitude above ground: {altitudeAboveGround}");
                    if (altitudeAboveGround < minAltitudeAboveGround) {
                        minAltitudeAboveGround = altitudeAboveGround;
                    }
                }
            }
            
            Log($"=> Min altitude above ground: {minAltitudeAboveGround}");
            Log("--------------------------------");
            return minAltitudeAboveGround;
        }

        private Vector3[] GetColliderCorners(Collider collider) {
            Log($"GetColliderCorners: {collider.name}");
            if (collider is BoxCollider boxCollider) {
                Log($"=> It's a BoxCollider");
                
                Vector3 center = boxCollider.center;
                Log($"   - Center: {center}");
                Vector3 size = boxCollider.size;
                Log($"   - Size: {size}");
                
                // Calculer les 8 coins du box collider dans l'espace local
                Vector3[] localCorners = new Vector3[] {
                    center + new Vector3(-size.x, -size.y, -size.z) * 0.5f,     // En bas à gauche
                    center + new Vector3(size.x, -size.y, -size.z) * 0.5f,      // En bas à droite
                    center + new Vector3(-size.x, size.y, -size.z) * 0.5f,      // En haut à gauche
                    center + new Vector3(size.x, size.y, -size.z) * 0.5f,       // En haut à droite
                    center + new Vector3(-size.x, -size.y, size.z) * 0.5f,      // En haut à gauche
                    center + new Vector3(size.x, -size.y, size.z) * 0.5f,       // En bas à droite
                    center + new Vector3(-size.x, size.y, size.z) * 0.5f,       // En haut à droite
                    center + new Vector3(size.x, size.y, size.z) * 0.5f         // En bas à droite
                };
                
                // Transformer chaque point dans l'espace monde en tenant compte de la rotation
                Vector3[] corners = new Vector3[localCorners.Length];
                for (int i = 0; i < localCorners.Length; i++) {
                    corners[i] = collider.transform.TransformPoint(localCorners[i]);
                    Log($"  - Corner {i}: {localCorners[i]} => {corners[i]}");
                }
                
                return corners;
            }
            else if (collider is SphereCollider sphereCollider) {
                Log($"- SphereCollider: {sphereCollider.name}");
                
                // Pour un sphere collider, on vérifie le point le plus bas
                Vector3 center = sphereCollider.center;
                Log($"  - center: {center}");
                float radius = sphereCollider.radius;
                Log($"  - radius: {radius}");
                
                // Calculer le point le plus bas dans l'espace local
                Vector3 localBottomPoint = center + new Vector3(0, -radius, 0);
                Log($"  - localBottomPoint: {localBottomPoint}");
                
                // Transformer dans l'espace monde
                Vector3 bottomPoint = collider.transform.TransformPoint(localBottomPoint);
                return new Vector3[] { bottomPoint };
            }
            else if (collider is CapsuleCollider capsuleCollider) {
                Log($"- CapsuleCollider: {capsuleCollider.name}");

                // Pour un capsule collider, on vérifie les points les plus bas
                Vector3 center = capsuleCollider.center;
                Log($"  - center: {center}");
                float radius = capsuleCollider.radius;
                Log($"  - radius: {radius}");
                float height = capsuleCollider.height;
                Log($"  - height: {height}");
                
                // Calculer les points les plus bas dans l'espace local
                Vector3[] localPoints = new Vector3[] {
                    center + new Vector3(-radius, -height * 0.5f, 0),
                    center + new Vector3(radius, -height * 0.5f, 0)
                };
                
                Vector3[] points = new Vector3[2];
                for (int i = 0; i < localPoints.Length; i++) {
                    points[i] = collider.transform.TransformPoint(localPoints[i]);
                    Log($"  - localPoint {i}: {localPoints[i]} => {points[i]}");
                }
                
                return points;
            }
            
            // Pour les autres types de colliders, on vérifie juste le centre
            Log($"- Other collider: {collider.name}");
            return new Vector3[] { collider.transform.position };
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part) {
            Log($"onEditorPartEvent: {eventType} - {part.name}:{part.persistentId}");

            double altitudeAboveGround = GetAltitudeAboveGround(part);
            if (altitudeAboveGround < 0) {
                Log($"Part {part.name} is below ground of {altitudeAboveGround}m");
                float heightToAdd = (float) altitudeAboveGround * -1.0f + 0.1f;
                
                part.transform.position += part.transform.up * heightToAdd;
                Log($"=> New position above ground: {part.transform.position}");
            }
        }
    }
}
