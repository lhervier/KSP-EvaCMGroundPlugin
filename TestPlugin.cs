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

        private Vector3[] GetColliderCorners(Collider collider) {
            if (collider is BoxCollider boxCollider) {
                Log($"=> BoxCollider (center: {boxCollider.center}, size: {boxCollider.size})");
                
                Vector3 center = boxCollider.center;
                Vector3 size = boxCollider.size;
                
                // Calculer les 8 coins du box collider dans l'espace local
                // On est sûr que notre objet est à l'intérieur de l'espace défini par ces 8 points.
                return new Vector3[] {
                    center + new Vector3(-size.x, -size.y, -size.z) * 0.5f,     // En bas à gauche
                    center + new Vector3(size.x, -size.y, -size.z) * 0.5f,      // En bas à droite
                    center + new Vector3(-size.x, size.y, -size.z) * 0.5f,      // En haut à gauche
                    center + new Vector3(size.x, size.y, -size.z) * 0.5f,       // En haut à droite
                    center + new Vector3(-size.x, -size.y, size.z) * 0.5f,      // En haut à gauche
                    center + new Vector3(size.x, -size.y, size.z) * 0.5f,       // En bas à droite
                    center + new Vector3(-size.x, size.y, size.z) * 0.5f,       // En haut à droite
                    center + new Vector3(size.x, size.y, size.z) * 0.5f         // En bas à droite
                };
            }
            else if (collider is SphereCollider sphereCollider) {
                Log($"- SphereCollider: {sphereCollider.name} (center: {sphereCollider.center}, radius: {sphereCollider.radius})");
                
                // Pour un sphere collider, on vérifie le point le plus bas
                Vector3 center = sphereCollider.center;
                float radius = sphereCollider.radius;
                
                // Calculer le point le plus bas dans l'espace local
                // FIXME: On ne prend pas en compte la rotation du collider
                return new Vector3[] { center + new Vector3(0, -radius, 0) };
            }
            else if (collider is CapsuleCollider capsuleCollider) {
                Log($"- CapsuleCollider: {capsuleCollider.name} (center: {capsuleCollider.center}, radius: {capsuleCollider.radius}, height: {capsuleCollider.height})");

                // Pour un capsule collider, on vérifie les points les plus bas
                // FIXME: On ne prend pas en compte la rotation du collider
                Vector3 center = capsuleCollider.center;
                float radius = capsuleCollider.radius;
                float height = capsuleCollider.height;
                
                // Calculer les points les plus bas dans l'espace local
                return new Vector3[] {
                    center + new Vector3(-radius, -height * 0.5f, 0),
                    center + new Vector3(radius, -height * 0.5f, 0)
                };
            }
            
            // Pour les autres types de colliders, on vérifie juste le centre
            Log($"- Other collider: {collider.name} (position: {collider.transform.position})");
            return new Vector3[] { collider.transform.position };
        }

        private double GetAltitudeAboveGround(Part part) {
            Log("--------------------------------");
            Log($"GetAltitudeAboveGround: {part.name}:{part.persistentId}");
            
            Vector3d worldPosition = part.transform.position;
            Quaternion worldRotation = part.transform.rotation;
            Vector3d worldForward = worldRotation * Vector3.forward;
            Vector3d worldUp = worldRotation * Vector3.up;
            Vector3d worldRight = worldRotation * Vector3.right;
            Log($"- World (Relative to Unity scene) :");
            Log($"  - Position: {worldPosition}");
            Log($"  - Rotation (Euler): {worldRotation.eulerAngles}");
            Log($"  - Forward: {worldForward}");
            Log($"  - Up: {worldUp}");
            Log($"  - Right: {worldRight}");
            
            Vector3d localPosition = part.transform.localPosition;
            Quaternion localRotation = part.transform.localRotation;
            Vector3d localForward = localRotation * Vector3.forward;
            Vector3d localUp = localRotation * Vector3.up;
            Vector3d localRight = localRotation * Vector3.right;
            Log($"- From Parent:");
            Log($"  - Position: {localPosition}");
            Log($"  - Rotation (Euler): {localRotation.eulerAngles}");
            Log($"  - Forward: {localForward}");
            Log($"  - Up: {localUp}");
            Log($"  - Right: {localRight}");
            
            double latitude = FlightGlobals.currentMainBody.GetLatitude(worldPosition);
            double longitude = FlightGlobals.currentMainBody.GetLongitude(worldPosition);
            double altitude = FlightGlobals.currentMainBody.GetAltitude(worldPosition);
            Log($"- KSP position (Relative to main body) : ");
            Log($"  - Latitude: {latitude}");
            Log($"  - Longitude: {longitude}");
            Log($"  - Altitude: {altitude}");


            Log($"- Terrain altitude: {FlightGlobals.currentMainBody.TerrainAltitude(latitude, longitude, true)}");

            // Obtenir tous les colliders de la pièce
            Collider[] colliders = part.GetComponentsInChildren<Collider>();
            
            double minAltitudeAboveGround = float.MaxValue;

            foreach (Collider collider in colliders) {
                Log($"Collider: {collider.name}");

                // Obtenir les points de collision (les coins du collider)
                // Dans les coordonnées locales
                Vector3[] corners = GetColliderCorners(collider);
                foreach (Vector3 corner in corners) {
                    Log($"- Corner (local): {corner}");
                    Vector3 worldCorner = collider.transform.TransformPoint(corner);
                    Log($"  - Corner (world): {worldCorner}");

                    // Convertir la position en coordonnées géographiques
                    double cornerLatitude = FlightGlobals.currentMainBody.GetLatitude(worldCorner);
                    double cornerLongitude = FlightGlobals.currentMainBody.GetLongitude(worldCorner);
                    double cornerAltitude = FlightGlobals.currentMainBody.GetAltitude(worldCorner);
                    Log($"  - Latitude: {cornerLatitude} - Longitude: {cornerLongitude} - Altitude: {cornerAltitude}");
                    
                    // Obtenir la hauteur du terrain à cette position
                    double terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(cornerLatitude, cornerLongitude, true);
                    Log($"  - Terrain altitude: {terrainAltitude}");

                    // Si un seul point est sous le terrain, la pièce est considérée comme sous terre
                    double altitudeAboveGround = cornerAltitude - terrainAltitude;
                    Log($"  => Altitude above ground: {altitudeAboveGround}");
                    if (altitudeAboveGround < minAltitudeAboveGround) {
                        minAltitudeAboveGround = altitudeAboveGround;
                    }
                }
            }
            
            Log($"=> Min altitude above ground: {minAltitudeAboveGround}");
            Log("--------------------------------");
            return minAltitudeAboveGround;
        }

        private void OnEditorPartEvent(ConstructionEventType eventType, Part part) {
            Log($"onEditorPartEvent: {eventType} - {part.name}:{part.persistentId}");

            double altitudeAboveGround = GetAltitudeAboveGround(part);
            if (altitudeAboveGround < 0) {
                Log($"Part {part.name} is below ground of {altitudeAboveGround}m");
                float heightToAdd = (float) altitudeAboveGround * -1.0f + 0.1f;     // On prend une toute petite marge
                Log($"  - heightToAdd: {heightToAdd}");
                Log($"  - Actual part position (below ground): {part.transform.position}");
                
                // Ajoute la hauteur à la position de la pièce
                part.transform.position += FlightGlobals.currentMainBody.transform.up * heightToAdd;
                
                Log($"=> New position (sticked to the ground): {part.transform.position}");
            }
        }
    }
}
