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
    public class DeathBeamOptions : ThunderScript
    {
        [ModOption(name: "Traditional Cast", tooltip: "Hold trigger to charge, throw to cast", valueSourceName: nameof(booleanOption), defaultValueIndex = 1)]
        public static bool TraditionalCast = false;
        [ModOption(name: "Beam Damage", tooltip: "The base damage", valueSourceName: nameof(singleValues), defaultValueIndex = 25, order = 1)]
        public static float BeamDamage = 25;
        [ModOption(name: "Vitals Damage Multiplier", tooltip: "Multiplies damage when hitting the head or torso", valueSourceName: nameof(tenthsValues), defaultValueIndex = 20, order = 2)]
        public static float VitalsMultiplier = 2;
        [ModOption(name: "Explosion Damage", tooltip: "The explosion damage", valueSourceName: nameof(singleValues), defaultValueIndex = 10, order = 3)]
        public static float ExplosionDamage = 10;
        [ModOption(name: "Explosion Radius", tooltip: "The radius of the explosion", valueSourceName: nameof(singleValues), defaultValueIndex = 5, order = 4)]
        public static float ExplosionRadius = 5;
        [ModOption(name: "Explosion Force", tooltip: "The force of the explosion", valueSourceName: nameof(singleValues), defaultValueIndex = 10, order = 5)]
        public static float ExplosionForce = 10;
        public static ModOptionBool[] booleanOption =
        {
            new ModOptionBool("Enabled", true),
            new ModOptionBool("Disabled", false)
        };
        public static ModOptionFloat[] singleValues()
        {
            ModOptionFloat[] modOptionFloats = new ModOptionFloat[1001];
            float num = 0f;
            for (int i = 0; i < modOptionFloats.Length; ++i)
            {
                modOptionFloats[i] = new ModOptionFloat(num.ToString("0"), num);
                num += 1f;
            }
            return modOptionFloats;
        }
        public static ModOptionFloat[] tenthsValues()
        {
            ModOptionFloat[] modOptionFloats = new ModOptionFloat[1001];
            float num = 0f;
            for (int i = 0; i < modOptionFloats.Length; ++i)
            {
                modOptionFloats[i] = new ModOptionFloat(num.ToString("0.0"), num);
                num += 0.1f;
            }
            return modOptionFloats;
        }
    }
    public class DeathBeamSpell : SpellCastCharge
    {
        Item beam;
        LineRenderer line;
        bool isFiring = false;
        bool isShooting = false;
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
            handPose = Catalog.GetData<HandPoseData>("Pointing");
            shootData = Catalog.GetData<EffectData>("DeathBeamShoot");
            hitData = Catalog.GetData<EffectData>("HitLightningBolt");
            explosionData = Catalog.GetData<EffectData>("MeteorExplosion");
            itemData = Catalog.GetData<ItemData>("DeathBeam");
            materialData = Catalog.GetData<MaterialData>("Projectile");
        }
        public override void Unload()
        {
            base.Unload();
            spellCaster.AllowSpellWheel(this);
        }
        public override void Fire(bool active)
        {
            base.Fire(active);
            if (spellCaster.mana.creature.player != null && !DeathBeamOptions.TraditionalCast) return;
            if (active)
            {
                itemData.SpawnAsync(item =>
                {
                    beam = item;
                    beam.physicBody.isKinematic = true;
                    line = beam.GetCustomReference("Line").GetComponent<LineRenderer>();
                    beam.GetCustomReference("Sphere").GetComponent<Renderer>().material.SetColor("_BaseColor", beamColor);
                    line.material.SetColor("_BaseColor", beamColor);
                    beam.renderers[0].GetComponent<Light>().color = beamColor / beamColor.maxColorComponent;
                }, spellCaster.ragdollHand.fingerIndex.tip.position + (spellCaster.ragdollHand.fingerIndex.tip.forward.normalized * 0.03f), Quaternion.identity, null, false);
                isFiring = true;
                spellCaster.ragdollHand.poser.SetDefaultPose(handPose);
                spellCaster.ragdollHand.poser.SetTargetPose(handPose);
                spellCaster.ragdollHand.poser.SetTargetWeight(1.0f); 
                spellCaster.DisableSpellWheel(this);
            }
            if (!active && beam != null)
            {
                GameManager.local.StartCoroutine(FadeOut(beam, line));
                beam = null;
                isFiring = false;
                spellCaster.ragdollHand.poser.ResetDefaultPose();
                spellCaster.ragdollHand.poser.ResetTargetPose();
            }
            if(!active) spellCaster.AllowSpellWheel(this);
        }
        public override void Throw(Vector3 velocity)
        {
            base.Throw(velocity);
            if (spellCaster.mana.creature.player != null && !DeathBeamOptions.TraditionalCast) return;
            if (beam != null)
            {
                Shoot(velocity);
                spellCaster.AllowSpellWheel(this);
            }
        }
        public void Shoot(Vector3 direction)
        {
            GameObject sound = new GameObject("Sound");
            sound.transform.position = beam.transform.position;
            EffectInstance shoot = shootData.Spawn(sound.transform, null, true);
            shoot.SetIntensity(1f);
            shoot.Play();
            GameObject.Destroy(sound, 5);
            RaycastHit[] hits = Physics.RaycastAll(beam.transform.position, direction, Mathf.Infinity, -1, QueryTriggerInteraction.Collide);
            line.SetPosition(0, beam.transform.position);
            line.SetPosition(1, beam.transform.position + direction * 1000);
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
                if (spellCaster?.ragdollHand?.playerHand?.controlHand != null && spellCaster.ragdollHand.playerHand.controlHand.alternateUsePressed)
                    Impact(wall.point, wall.normal, wall.transform.up);
                EffectInstance instance = hitData.Spawn(wall.point, Quaternion.LookRotation(wall.normal), null, new CollisionInstance(new DamageStruct(DamageType.Energy, 25)), true, null, false);
                instance.SetIntensity(1f);
                instance.Play();
                line.SetPosition(1, wall.point);
            }
            foreach (RaycastHit hit in creatureHits)
            {
                if (!hitWall || (hitWall && Vector3.Distance(hit.point, beam.transform.position) < Vector3.Distance(wall.point, beam.transform.position)))
                {
                    CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, DeathBeamOptions.BeamDamage), materialData);
                    collision.contactPoint = hit.point;
                    collision.contactNormal = hit.normal;
                    collision.casterHand = spellCaster;
                    collision.targetCollider = hit.collider;
                    collision.targetColliderGroup = hit.collider.GetComponentInParent<ColliderGroup>();
                    EffectInstance instance;
                    if (hit.collider.GetComponentInParent<RagdollPart>() is RagdollPart part)
                    {
                        instance = hitData.Spawn(hit.point, Quaternion.LookRotation(hit.normal), collision.targetColliderGroup ? collision.targetColliderGroup.transform : null, collision, true, null, false);
                        instance.SetIntensity(1f);
                        instance.Play();
                        collision.damageStruct.hitRagdollPart = part;
                        if (part.type == RagdollPart.Type.Head || part.type == RagdollPart.Type.Neck || part.type == RagdollPart.Type.Torso) collision.damageStruct.baseDamage *= DeathBeamOptions.VitalsMultiplier;
                        part.ragdoll.creature.Damage(collision);
                        part.ragdoll.creature.TryPush(Creature.PushType.Hit, hit.transform.forward, 1, part.type);
                        part.physicBody.AddForceAtPosition((hit.point - beam.transform.position).normalized, hit.point, ForceMode.Impulse);
                        part.ragdoll.creature.lastInteractionTime = Time.time;
                        part.ragdoll.creature.lastInteractionCreature = spellCaster.mana.creature;
                    }
                    else if (hit.collider.GetComponentInParent<Creature>() is Creature creature)
                    {
                        instance = hitData.Spawn(hit.transform, null, true);
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
                CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, DeathBeamOptions.BeamDamage), materialData);
                collision.contactPoint = hit.point;
                collision.contactNormal = hit.normal;
                collision.casterHand = spellCaster;
                collision.targetCollider = hit.collider;
                collision.targetColliderGroup = hit.collider.GetComponentInParent<ColliderGroup>();
                Breakable breakable = hit.collider.GetComponentInParent<Breakable>();
                if (breakable != null)
                {
                    --breakable.hitsUntilBreak;
                    if (breakable.canInstantaneouslyBreak)
                        breakable.hitsUntilBreak = 0;
                    breakable.onTakeDamage?.Invoke(0);
                    if (!breakable.IsBroken && breakable.hitsUntilBreak == 0)
                        breakable.Break();
                }
                hit.collider.attachedRigidbody?.AddForceAtPosition((hit.point - beam.transform.position).normalized, hit.point, ForceMode.Impulse);
                EffectInstance instance = hitData.Spawn(hit.point, Quaternion.LookRotation(hit.normal), collision.targetColliderGroup ? collision.targetColliderGroup.transform : null, collision, true, null, false);
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
            if (color.a > 1) color.a = 1;
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
            if (beam != null && isFiring)
            {
                beam.transform.position = spellCaster.ragdollHand.fingerIndex.tip.position + (spellCaster.ragdollHand.fingerIndex.tip.forward.normalized * 0.03f);
                line.SetPosition(0, beam.transform.position);
                line.SetPosition(1, beam.transform.position);
                if(beamColor.a > 1) beamColor.a = 1;
            }
            if (spellCaster.mana.creature.player == null || (spellCaster.mana.creature.player != null && DeathBeamOptions.TraditionalCast)) return;
            if (PlayerControl.GetHand(spellCaster.side).gripPressed) spellCaster.DisableSpellWheel(this);
            else spellCaster.AllowSpellWheel(this);
            if(PlayerControl.GetHand(spellCaster.side).gripPressed && !isFiring && spellCaster.ragdollHand.grabbedHandle == null && spellCaster.telekinesis.catchedHandle == null)
            {
                itemData.SpawnAsync(item =>
                {
                    beam = item;
                    beam.physicBody.isKinematic = true;
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
                Shoot(-spellCaster.ragdollHand.fingerIndex.distal.mesh.right);
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
            Collider[] sphereContacts = Physics.OverlapSphere(contactPoint, DeathBeamOptions.ExplosionRadius, 232799233);
            List<Creature> creaturesPushed = new List<Creature>();
            List<Rigidbody> rigidbodiesPushed = new List<Rigidbody>();
            creaturesPushed.Add(Player.local.creature);
            foreach (Creature creature in Creature.allActive)
            {
                if (!creature.isPlayer && !creature.isKilled && Vector3.Distance(contactPoint, creature.transform.position) < DeathBeamOptions.ExplosionRadius && !creaturesPushed.Contains(creature))
                {
                    CollisionInstance collision = new CollisionInstance(new DamageStruct(DamageType.Energy, DeathBeamOptions.ExplosionForce));
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
                Breakable breakable = collider.attachedRigidbody?.GetComponentInParent<Breakable>();
                if (breakable != null)
                {
                    if (!breakable.IsBroken && breakable.canInstantaneouslyBreak)
                        breakable.Break();
                    for (int index = 0; index < breakable.subBrokenItems.Count; ++index)
                    {
                        Rigidbody rigidBody = breakable.subBrokenItems[index].physicBody.rigidBody;
                        if (rigidBody && !rigidbodiesPushed.Contains(rigidBody))
                        {
                            rigidBody.AddExplosionForce(DeathBeamOptions.ExplosionForce, contactPoint, DeathBeamOptions.ExplosionRadius, 0.5f, ForceMode.VelocityChange);
                            rigidbodiesPushed.Add(rigidBody);
                        }
                    }
                    for (int index = 0; index < breakable.subBrokenBodies.Count; ++index)
                    {
                        PhysicBody subBrokenBody = breakable.subBrokenBodies[index];
                        if (subBrokenBody && !rigidbodiesPushed.Contains(subBrokenBody.rigidBody))
                        {
                            subBrokenBody.rigidBody.AddExplosionForce(DeathBeamOptions.ExplosionForce, contactPoint, DeathBeamOptions.ExplosionRadius, 0.5f, ForceMode.VelocityChange);
                            rigidbodiesPushed.Add(subBrokenBody.rigidBody);
                        }
                    }
                }
                if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic && Vector3.Distance(contactPoint, collider.transform.position) < DeathBeamOptions.ExplosionRadius)
                {
                    if (collider.attachedRigidbody.gameObject.layer != GameManager.GetLayer(LayerName.NPC) && !rigidbodiesPushed.Contains(collider.attachedRigidbody))
                    {
                        collider.attachedRigidbody.AddExplosionForce(DeathBeamOptions.ExplosionForce, contactPoint, DeathBeamOptions.ExplosionRadius, 0.5f, ForceMode.VelocityChange);
                        rigidbodiesPushed.Add(collider.attachedRigidbody);
                    }
                }
            }
        }
    }
}
