using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace DeathBeam
{
    public class DeathBeamSpell : SpellCastData
    {
        Item beam;
        LineRenderer line;
        public float beamDamage = 25;
        public float vitalsMultiplier = 2;
        public float explosionDamage = 10;
        public float explosionRadius = 5;
        public float explosionForce = 10;
        bool isFiring = false;
        bool isShooting = false;
        SpellCaster spellCaster;
        HandPoseData handPose;
        EffectData shootData;
        EffectData hitData;
        EffectData explosionData;
        ItemData itemData;
        MaterialData materialData;
        public Color beamColor = new Color(47.75f, 0, 27.25f, 1);
        public override void Load(SpellCaster caster, Level level)
        {
            base.Load(caster, level);
            spellCaster = caster;
            handPose = Catalog.GetData<HandPoseData>("Pointing");
            shootData = Catalog.GetData<EffectData>("DeathBeamShoot");
            hitData = Catalog.GetData<EffectData>("HitLightningBolt");
            explosionData = Catalog.GetData<EffectData>("MeteorExplosion");
            itemData = Catalog.GetData<ItemData>("DeathBeam");
            materialData = Catalog.GetData<MaterialData>("Projectile");
        }
        public void Shoot()
        {
            GameObject sound = new GameObject("Sound");
            sound.transform.position = beam.transform.position;
            EffectInstance shoot = shootData.Spawn(sound.transform, true);
            shoot.SetIntensity(1f);
            shoot.Play();
            GameObject.Destroy(sound, 5);
            RaycastHit[] hits = Physics.RaycastAll(beam.transform.position, -spellCaster.ragdollHand.fingerIndex.distal.mesh.right, Mathf.Infinity, -1, QueryTriggerInteraction.Collide);
            line.SetPosition(0, beam.transform.position);
            line.SetPosition(1, beam.transform.position + -spellCaster.ragdollHand.fingerIndex.distal.mesh.right * 1000);
            bool hitWall = false;
            RaycastHit wall = new RaycastHit();
            List<RaycastHit> wallHits = new List<RaycastHit>();
            List<RaycastHit> creatureHits = new List<RaycastHit>();
            List<RaycastHit> itemHits = new List<RaycastHit>();
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.attachedRigidbody == null && !hit.collider.isTrigger && !wallHits.Contains(hit))
                {
                    wallHits.Add(hit);
                    hitWall = true;
                }
                if (hit.collider.attachedRigidbody != null && hit.collider.GetComponentInParent<Creature>() is Creature creature && !creatureHits.Contains(hit) && creature != spellCaster.mana.creature)
                {
                    if (hit.collider.GetComponentInParent<RagdollPart>() != null || (hit.collider.GetComponentInParent<RagdollPart>() == null && !creature.ragdoll.targetPart.gameObject.activeSelf))
                        creatureHits.Add(hit);
                }
                if (hit.collider.attachedRigidbody != null && hit.collider.GetComponentInParent<Item>() && !itemHits.Contains(hit))
                {
                    itemHits.Add(hit);
                }
            }
            if (wallHits.Count >= 1) wall = wallHits[0];
            foreach (RaycastHit hit in wallHits)
            {
                if (Vector3.Distance(hit.point, beam.transform.position) < Vector3.Distance(wall.point, beam.transform.position))
                {
                    wall = hit;
                }
            }
            if (hitWall)
            {
                if (spellCaster.ragdollHand.playerHand.controlHand.alternateUsePressed)
                    Impact(wall.point, wall.normal, wall.transform.up);
                EffectInstance instance = hitData.Spawn(wall.point, Quaternion.LookRotation(wall.normal), null, new CollisionInstance(new DamageStruct(DamageType.Energy, 25)), true, null, false, null);
                instance.SetIntensity(1f);
                instance.Play();
                line.SetPosition(1, wall.point);
            }
            foreach (RaycastHit hit in creatureHits)
            {
                if (!hitWall || (hitWall && Vector3.Distance(hit.point, beam.transform.position) < Vector3.Distance(wall.point, beam.transform.position)))
                {
                    CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, beamDamage), materialData);
                    collision.contactPoint = hit.point;
                    collision.contactNormal = hit.normal;
                    collision.casterHand = spellCaster;
                    collision.targetCollider = hit.collider;
                    collision.targetColliderGroup = hit.collider.GetComponentInParent<ColliderGroup>();
                    EffectInstance instance;
                    if (hit.collider.GetComponentInParent<RagdollPart>() is RagdollPart part)
                    {
                        instance = hitData.Spawn(hit.point, Quaternion.LookRotation(hit.normal), collision.targetColliderGroup ? collision.targetColliderGroup.transform : null, collision, true, null, false, null);
                        instance.SetIntensity(1f);
                        instance.Play();
                        collision.damageStruct.hitRagdollPart = part;
                        if (part.type == RagdollPart.Type.Head || part.type == RagdollPart.Type.Neck || part.type == RagdollPart.Type.Torso) collision.damageStruct.baseDamage *= vitalsMultiplier;
                        part.ragdoll.creature.Damage(collision);
                        part.ragdoll.creature.TryPush(Creature.PushType.Hit, hit.transform.forward, 1, part.type);
                        part.rb.AddForceAtPosition((hit.point - beam.transform.position).normalized, hit.point, ForceMode.Impulse);
                        part.ragdoll.creature.lastInteractionTime = Time.time;
                        part.ragdoll.creature.lastInteractionCreature = spellCaster.mana.creature;
                    }
                    else if (hit.collider.GetComponentInParent<Creature>() is Creature creature)
                    {
                        instance = hitData.Spawn(hit.transform, true);
                        instance.SetIntensity(1f);
                        instance.Play();
                        collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                        creature.Damage(collision);
                        creature.TryPush(Creature.PushType.Hit, hit.transform.forward, 1);
                        creature.lastInteractionTime = Time.time;
                        creature.lastInteractionCreature = spellCaster.mana.creature;
                    }
                }
            }
            foreach (RaycastHit hit in itemHits)
            {
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, beamDamage), materialData);
                collision.contactPoint = hit.point;
                collision.contactNormal = hit.normal;
                collision.casterHand = spellCaster;
                collision.targetCollider = hit.collider;
                collision.targetColliderGroup = hit.collider.GetComponentInParent<ColliderGroup>();
                hit.collider.attachedRigidbody.AddForceAtPosition((hit.point - beam.transform.position).normalized, hit.point, ForceMode.Impulse);
                EffectInstance instance = hitData.Spawn(hit.point, Quaternion.LookRotation(hit.normal), collision.targetColliderGroup ? collision.targetColliderGroup.transform : null, collision, true, null, false, null);
                instance.SetIntensity(1f);
                instance.Play();
            }
            GameManager.local.StartCoroutine(FadeOut(beam, line));
            beam = null;
            isFiring = false;
        }
        public IEnumerator FadeOut(Item item, LineRenderer lineRenderer)
        {
            Renderer renderer = item.renderers[0];
            Color color = beamColor;
            while (color.a > 0)
            {
                color.a -= Time.deltaTime * 2;
                renderer.material.color = color;
                lineRenderer.material.color = color;
                yield return null;
            }
            item.Despawn();
            yield break;
        }
        public override void UpdateCaster()
        {
            base.UpdateCaster();
            if (PlayerControl.GetHand(spellCaster.side).gripPressed) spellCaster.DisableSpellWheel(this);
            else spellCaster.AllowSpellWheel(this);
            if (beam != null && isFiring)
            {
                beam.transform.position = spellCaster.ragdollHand.fingerIndex.tip.position + (spellCaster.ragdollHand.fingerIndex.tip.forward.normalized * 0.03f);
                line.SetPosition(0, beam.transform.position);
                line.SetPosition(1, beam.transform.position);
            }
            if(PlayerControl.GetHand(spellCaster.side).gripPressed && !isFiring && spellCaster.ragdollHand.grabbedHandle == null && spellCaster.telekinesis.catchedHandle == null)
            {
                itemData.SpawnAsync(item =>
                {
                    beam = item;
                    beam.rb.isKinematic = true;
                    line = beam.GetCustomReference("Line").GetComponent<LineRenderer>();
                    beam.GetCustomReference("Sphere").GetComponent<Renderer>().material.SetColor("_BaseColor", beamColor);
                    line.material.SetColor("_BaseColor", beamColor);
                    beam.renderers[0].GetComponent<Light>().color = beamColor/beamColor.maxColorComponent;
                }, spellCaster.ragdollHand.fingerIndex.tip.position + (spellCaster.ragdollHand.fingerIndex.tip.forward.normalized * 0.03f), Quaternion.identity, null, false);
                isFiring = true;
                spellCaster.ragdollHand.poser.SetDefaultPose(handPose);
                spellCaster.ragdollHand.poser.SetTargetPose(handPose); 
                spellCaster.ragdollHand.poser.SetTargetWeight(1.0f);
            }
            if (!PlayerControl.GetHand(spellCaster.side).gripPressed && beam != null)
            {
                GameManager.local.StartCoroutine(FadeOut(beam, line));
                beam = null;
                isFiring = false;
                spellCaster.ragdollHand.poser.ResetDefaultPose();
                spellCaster.ragdollHand.poser.ResetTargetPose();
            }
            if(PlayerControl.GetHand(spellCaster.side).usePressed && isFiring && beam != null && !isShooting)
            {
                Shoot();
                isShooting = true;
            }
            if (!PlayerControl.GetHand(spellCaster.side).usePressed && isShooting)
            {
                isShooting = false;
            }
        }
        private void Impact(Vector3 contactPoint, Vector3 contactNormal, Vector3 contactNormalUpward)
        {
            EffectInstance effectInstance = explosionData.Spawn(contactPoint, Quaternion.LookRotation(-contactNormal, contactNormalUpward));
            effectInstance.SetIntensity(1f);
            effectInstance.Play();
            Collider[] sphereContacts = Physics.OverlapSphere(contactPoint, explosionRadius, 232799233);
            List<Creature> creaturesPushed = new List<Creature>();
            List<Rigidbody> rigidbodiesPushed = new List<Rigidbody>();
            creaturesPushed.Add(Player.local.creature);
            foreach (Creature creature in Creature.allActive)
            {
                if (!creature.isPlayer && !creature.isKilled && Vector3.Distance(contactPoint, creature.transform.position) < explosionRadius && !creaturesPushed.Contains(creature))
                {
                    CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, explosionForce));
                    collision.damageStruct.hitRagdollPart = creature.ragdoll.rootPart;
                    collision.casterHand = spellCaster;
                    creature.Damage(collision);
                    creature.ragdoll.SetState(Ragdoll.State.Destabilized);
                    creature.lastInteractionTime = Time.time;
                    creature.lastInteractionCreature = spellCaster.mana.creature;
                    creaturesPushed.Add(creature);
                }
            }
            foreach (Collider collider in sphereContacts)
            {
                if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic && Vector3.Distance(contactPoint, collider.transform.position) < explosionRadius)
                {
                    if (collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.NPC) && !rigidbodiesPushed.Contains(collider.attachedRigidbody))
                    {
                        collider.attachedRigidbody.AddExplosionForce(explosionForce, contactPoint, explosionRadius, 0.5f, ForceMode.VelocityChange);
                        rigidbodiesPushed.Add(collider.attachedRigidbody);
                    }
                }
            }
        }
    }
}
