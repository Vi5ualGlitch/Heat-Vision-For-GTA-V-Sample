using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TScriptHelper;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Drawing;
using NAudio.Gui;
using NAudio.Utils;
using NAudio.MediaFoundation;
using System.Diagnostics;
using NAudio.Wave;
using System.Reflection.Emit;
using static System.Net.Mime.MediaTypeNames;

using System.Net;
using NAudio.CoreAudioApi;

//  This code will not compile as needed files aren't attached. 
//  It does however show the complete implementation of Laser into GTA V.
//  You still need more knowledge, for example creating a laser particle and animation it.
namespace Martian_Manhunter_V2
{
    public class martianVision : Script
    {

        public static Prop laser1, laser2;//  Laser 1 is left eye, Laser 2 is right eye

        /*
        
            5 aux props to control the rotation of the laser. 
            0 is attached to the head
            1 is attached to Left Eye
            2 is physically attached to 1/left eye
            3 is attached to the right eye
            4 is physically attached to 3/right eye

        */

        //  Occasionally I will consider laser1 and laser2 together. For example, isLaserEnding, I'm assuming laser2 is playing same end anim

        public static Prop[] aux = [null, null, null, null, null];

        public static Vector3 smoothedRotationLeft = Vector3.Zero, smoothedRotationRight = Vector3.Zero;//    is the rotation variable of the laser

        int lookBackTimer;//    timer when looking behind, clicking C to reset back to gamecamera rotation

        Model disintegrateLaserModel = new Model("mm_blash_beam");//mm_blash_beam mm_laser_beam

        Model blastLaserModel = new Model("mm_laser_beam");//mm_blash_beam mm_laser_beam


        public static int[] sizzleParticle = [0, 0], hazeParticle = [0, 0];//  sizzle and haze particle, for each eye/laser. 0 is left, 1 is right

        public static int steamParticle;//    steam particle

        public static int laserEndTimer;//    timer to delete the laser after end animation has played for the prop

        public static bool endLaserOnce;//    delete it once after the timer is done

        bool[] laserSoundsOnce = [false, false, false];//   each bool will play one laser sound once: start, loop, end

        int laserLoopStartTimer;//  a timer for when laser loop should play, around 1 second after start sound

        bool applyCamShakeOnce;//   once user presses R, a shake will happen at first laser contact/impact

        public static int[] sparkParticles = [0, 0];//    looped spark particle from laser
        int sparkParticleTimer = 0;//   timer to start the second spark particle, when first spark stops, this starts

        enum VisionType
        {
            slice,//    slices ped in half
            Blast,//    just burns ped
            Disintegrate,// burns ped into skeleton
            xRay//  not implemented yet

        }

        VisionType visionType = VisionType.Blast;// initial blast mode

        public static Vector3[] deleteRot = [Thelper.Zero, Thelper.Zero];//   used to maintain the last known rotation once R is released

        Dictionary<Entity, int> entsOnFire = new Dictionary<Entity, int>();

        Dictionary<int, int> posOnFire = new Dictionary<int, int>();//  gets the ID of fire and timer for it. Auto deletes after certain # of seconds

        List<int> fireIDs = new List<int>();//  this will remove the fireID if it exceeds max script amount: 125. Overrides auto deletion

        List<int> idToRemove = new List<int>();//   removes ID from dictionary

        int removeID = 0;// for the fire that spawns with heat vision, need to store it so I can remove the fire later.

        List<Entity> entsToRemove = new List<Entity>();//   temp list that removes items from dictionary of fire entities

        Vector3 laserLeftPosAttachOffset = new Vector3(0.06f, 0.07f, 0.03f);// 0.05f, 0.1f, 0.03f

        Vector3 laserRightPosAttachOffset = new Vector3(0.06f, 0.07f, -0.03f);//    laser offsets, varies based off each ped

        public martianVision()
        {

            Thelper.requestPTFXAsset("cut_chinese1");// request these when the program loads in

            Thelper.requestPTFXAsset("zefgtav_mm_laser");// request these when the program loads in

            Tick += MartianVision_Tick;

        }

        //  we require multiple auxilliary props to control rotation of the laser, these 
        //  aux props require collision but we need to make sure they don't collide with each other
        void setNoCollision()
        {

            for (int i = 0; i < aux.Length; i++)
            {

                if (aux[i] != null)
                {

                    foreach (Prop p in World.GetNearbyProps(aux[i].Position, 3f, [aux[i].Model]))
                    {
                        Main.martianManhunter.SetNoCollision(p, false);

                        if (p != aux[i])
                        {
                            aux[i].SetNoCollision(p, false);
                        }



                        Function.Call(Hash.SET_GAMEPLAY_CAM_IGNORE_ENTITY_COLLISION_THIS_UPDATE, aux[i]);// camera doesn't interact with prop

                    }

                }

            }


            /* foreach (Prop p in World.GetNearbyProps(Main.martianManhunter.Position, 5f, ["ng_proc_cigarette01a"]))
             {

                 Main.martianManhunter.SetNoCollision(p, true);

                 p.SetNoCollision(Main.martianManhunter, true);

                 Function.Call(Hash.SET_GAMEPLAY_CAM_IGNORE_ENTITY_COLLISION_THIS_UPDATE, p);


             }*/

        }

        // 0 = left, 1 = right, handles all of the laser attachments and rotation code
        void laserAttachments(string type)
        {

            if (Game.IsControlJustPressed(GTA.Control.LookBehind))
                lookBackTimer = Thelper.logTime();

            if (type == "spawn" && aux[0] != null && aux[1] != null && aux[2] != null && aux[3] != null && aux[4] != null)
            {

                aux[0].AttachTo(Main.martianManhunter.Bones[Bone.SkelHead], Thelper.Zero, Thelper.Zero, false, false, false, false, EulerRotationOrder.YXZ, true, false);

                aux[1].AttachTo(aux[0], new Vector3(0.05f, 0.1f, 0.03f), Thelper.Zero, false, false, false, false, EulerRotationOrder.YXZ, true);

                aux[2].AttachToBonePhysically(aux[1].Bones[0], Thelper.Zero, Thelper.Zero, Thelper.Zero, 1000f, false, true, false, true, EulerRotationOrder.YXZ);

                laser1.AttachTo(aux[2], default, default);

                aux[3].AttachTo(aux[0], new Vector3(0.05f, 0.1f, -0.03f), Thelper.Zero, false, false, false, false, EulerRotationOrder.YXZ, true);

                aux[4].AttachToBonePhysically(aux[3].Bones[0], Thelper.Zero, Thelper.Zero, Thelper.Zero, 1000f, false, true, false, true, EulerRotationOrder.YXZ);

                laser2.AttachTo(aux[4], default, new Vector3(0f, 0f, 0f));

                setNoCollision();

                Vector3 targetRotLeft = Thelper.DirToRot((raycast.hitPoint - aux[2].Position).Normalized);
                smoothedRotationLeft = targetRotLeft;

                Vector3 targetRotRight = Thelper.DirToRot((raycast.hitPoint - aux[4].Position).Normalized);
                smoothedRotationRight = targetRotRight;

                applyCamShakeOnce = true;

                laserSoundsOnce[0] = true;
                laserSoundsOnce[1] = true;
                laserLoopStartTimer = Thelper.logTime();

                playLaserAnimation(laser1, "start");

                playLaserAnimation(laser2, "start");

                if (flight.flightMode && !Thelper.isEntPlayingAnim(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip") && !Thelper.isEntPlayingAnim(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_laser_hold_a_clip"))
                {

                    bool playStartAnim = bool.Parse(Thelper.findSuitField(Main.iniLoc, "Flight Laser Idle Start Animation", "hi", true));

                    if (playStartAnim)
                        Thelper.playAnimControlTransition(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip", 4f, 1f, 0);
                    else
                        Thelper.playAnimControlTransition(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_laser_hold_a_clip", 4f, 1f, 1);

                }
            }

            else if (type == "tick")
            {

                for (int i = 0; i < aux.Length; i++)
                {

                    if (aux[i] != null)
                    {

                        Thelper.loopMissionEnt(aux[i]);//   tells game engine not to delete this prop for any reason

                        Thelper.editCollision(aux[i], false, true);

                    }

                }



                if (aux[0] != null && aux[1] != null && aux[2] != null && aux[3] != null && aux[4] != null && laser1 != null && laser2 != null)
                {

                    aux[0].AttachTo(Main.martianManhunter.Bones[Bone.SkelHead], Thelper.Zero, Thelper.Zero, false, false, false, false,
                        EulerRotationOrder.YXZ, true, false);

                    aux[1].AttachTo(aux[0], laserLeftPosAttachOffset, Thelper.Zero, false, false, false, false, EulerRotationOrder.YXZ, true);


                    aux[2].AttachToBonePhysically(aux[1].Bones[0], Thelper.Zero, Thelper.Zero, Thelper.Zero, 1000f, false, true, false, true, EulerRotationOrder.YXZ);

                    aux[3].AttachTo(aux[0], laserRightPosAttachOffset, Thelper.Zero, false, false, false, false, EulerRotationOrder.YXZ, true);

                    aux[4].AttachToBonePhysically(aux[3].Bones[0], Thelper.Zero, Thelper.Zero, Thelper.Zero, 1000f, false, true, false, true, EulerRotationOrder.YXZ);



                    laser1.AttachTo(aux[2], default, new Vector3(0f, 0f, 0f));

                    laser2.AttachTo(aux[4], default, new Vector3(0f, 0f, 0f));



                    if (!Game.IsControlPressed(GTA.Control.LookBehind) && Thelper.getMSPassed(lookBackTimer) > 2000)
                    {

                        Vector3 leftDir = (raycast.hitPoint - aux[2].Position).Normalized;


                        Main.correctFalseRotation(ref leftDir, ref smoothedRotationLeft);

                        Vector3 targetRotLeft = Thelper.DirToRot(leftDir);

                        Vector3 rightDir = (raycast.hitPoint - aux[4].Position).Normalized;

                        Main.correctFalseRotation(ref rightDir, ref smoothedRotationRight);


                        Vector3 targetRotRight = Thelper.DirToRot(rightDir);

                        float deltaTime = Game.LastFrameTime;

                        float smoothingSpeed = 17.0f;



                        if (aux[2].IsAttached() && !Thelper.isEntPlayingAnim(laser1, "mm_beam_animation", "end"))
                            aux[2].Rotation = smoothedRotationLeft;
                        else
                            aux[2].Rotation = deleteRot[0];

                        Game.Player.Character.Rotation = new Vector3(Main.martianManhunter.Rotation.X, smoothedRotationLeft.Y, smoothedRotationLeft.Z);

                        smoothedRotationLeft = Main.SmoothLerp(smoothedRotationLeft, targetRotLeft, smoothingSpeed, deltaTime);

                        Main.correctFalseRotation(ref leftDir, ref smoothedRotationLeft);

                        if (aux[4].IsAttached() && !Thelper.isEntPlayingAnim(laser2, "mm_beam_animation", "end"))
                            aux[4].Rotation = smoothedRotationRight;
                        else
                            aux[4].Rotation = deleteRot[1];

                        smoothedRotationRight = Main.SmoothLerp(smoothedRotationRight, targetRotRight, smoothingSpeed, deltaTime);

                        Main.correctFalseRotation(ref rightDir, ref smoothedRotationRight);



                    }
                    else
                    {

                        aux[2].Rotation = Main.martianManhunter.Rotation;

                        aux[4].Rotation = Main.martianManhunter.Rotation;


                    }

                }

            }

        }

        //  plays laser prop animation
        public static void playLaserAnimation(Prop laser, string animClip, float blendSpeed = 8f, bool loop = false)
        {

            if (!Thelper.hasAnimDictLoaded("mm_beam_animation"))
                Thelper.requestAnimDict("mm_beam_animation");

            if (laser != null)
                Thelper.playEntityAnim(laser, "mm_beam_animation", animClip, blendSpeed, loop, false, false, 0, 0);

        }
        //  stop laser props animation
        public static void stopLaserAnimation(Prop laser, string animClip, float stopSpeed)
        {

            if (!Thelper.hasAnimDictLoaded("mm_beam_animation"))
                Thelper.requestAnimDict("mm_beam_animation");

            if (laser != null)
                Thelper.stopEntityAnim(laser, "mm_beam_animation", animClip, stopSpeed);

        }

        //  handles laser particles, stopping and starting it
        public static void laserParticles(string type)
        {

            if (type == "spawn")
            {

                if (laser1 != null)
                {

                    if (!Thelper.hasPTFXAssetLoaded("zefgtav_mm_laser"))
                        Thelper.requestPTFXAsset("zefgtav_mm_laser");

                    if (!Thelper.doesPTFXLoopedExist(ref hazeParticle[0]))
                        hazeParticle[0] = Thelper.ptfxLoopedEntity("zefgtav_mm_laser", "hazer_idle", laser1, new Vector3(0f, -2.5f, 0f), new Vector3(0f, 0f, 0f), 1f, false, false, false);

                    if (!Thelper.doesPTFXLoopedExist(ref hazeParticle[1]))
                        hazeParticle[1] = Thelper.ptfxLoopedEntity("zefgtav_mm_laser", "hazer_idle", laser1, new Vector3(0f, -2.5f, 0f), new Vector3(0f, 0f, 0f), 1f, false, false, false);


                    if (!Thelper.doesPTFXLoopedExist(ref steamParticle))
                        steamParticle = Thelper.ptfxLoopedEntity("zefgtav_mm_laser", "steam", laser1, new Vector3(0.1f, 0.2f, 0f), new Vector3(0f, 0f, 0f), 0.5f, false, false, false);


                    if (!Thelper.hasPTFXAssetLoaded("cut_chinese1"))
                        Thelper.requestPTFXAsset("cut_chinese1");

                    if (!Thelper.doesPTFXLoopedExist(ref sizzleParticle[0]))
                        sizzleParticle[0] = Thelper.ptfxLoopedPedBone("cut_chinese1", "cs_cig_smoke", Main.martianManhunter, new Vector3(0.05f, 0.1f, -0.03f), Thelper.Zero, Bone.SkelHead, 4f);

                    if (!Thelper.doesPTFXLoopedExist(ref sizzleParticle[1]))
                        sizzleParticle[1] = Thelper.ptfxLoopedPedBone("cut_chinese1", "cs_cig_smoke", Main.martianManhunter, new Vector3(0.05f, 0.1f, 0.03f), Thelper.Zero, Bone.SkelHead, 4f);
                }

            }
            else if (type == "remove")
            {

                if (Thelper.doesPTFXLoopedExist(ref sparkParticles[0]))
                    Thelper.removeLoopedPTFX(ref sparkParticles[0]);

                if (Thelper.doesPTFXLoopedExist(ref sparkParticles[1]))
                    Thelper.removeLoopedPTFX(ref sparkParticles[1]);

                if (Thelper.doesPTFXLoopedExist(ref hazeParticle[0]))
                    Thelper.removeLoopedPTFX(ref hazeParticle[0]);

                if (Thelper.doesPTFXLoopedExist(ref hazeParticle[1]))
                    Thelper.removeLoopedPTFX(ref hazeParticle[1]);

                if (Thelper.doesPTFXLoopedExist(ref steamParticle))
                    Thelper.removeLoopedPTFX(ref steamParticle);

                if (Thelper.doesPTFXLoopedExist(ref sizzleParticle[0]))
                    Thelper.removeLoopedPTFX(ref sizzleParticle[0]);

                if (Thelper.doesPTFXLoopedExist(ref sizzleParticle[1]))
                    Thelper.removeLoopedPTFX(ref sizzleParticle[1]);

            }

        }

        //  spawns in the laser
        void spawnLaser()
        {

            if (!isAuxIntact())
            {
                for (int i = 0; i < aux.Length; i++)
                {

                    if (aux[i] == null)
                    {

                        aux[i] = Thelper.spawnProp("ng_proc_cigarette01a", Main.martianManhunter.Position.Around(5), true, false, false, GameplayCamera.Rotation);
                        aux[i].Opacity = 0;

                    }

                }
            }

            bool runOnce = false;

            if (isLaserEnding())
                deleteLaser("ragdoll");

            Model model = blastLaserModel;

            if (visionType == VisionType.Blast)
                model = blastLaserModel;

            else if (visionType == VisionType.Disintegrate)
                model = disintegrateLaserModel;

            if (laser1 == null)
            {

                laser1 = Thelper.spawnProp(model, Main.martianManhunter.Position, true, false, false, GameplayCamera.Rotation);
                runOnce = true;

            }

            if (laser2 == null)
            {

                laser2 = Thelper.spawnProp(model, Main.martianManhunter.Position, true, false, false, GameplayCamera.Rotation);
                runOnce = true;

            }

            laser1.Opacity = 0;
            laser2.Opacity = 0;

            if (runOnce)
            {

                laserAttachments("spawn");

                runOnce = false;

            }

        }

        //  remove a fire in range
        void removeScriptFireInRange()
        {

            for (int i = 0; i < fireIDs.Count; i++)
            {

                removeID = fireIDs[i];

                Thelper.removeScriptFire(ref removeID);

            }

        }

        //  handles the laser at every tick
        void laserTick()
        {
            //  if the laser raycast hits and object and it isn't a auxilliary prop
            bool hitEntNotAux(Entity ent)
            {
                return ent != null && ent != aux[0] && ent != aux[1] && ent != aux[2] && ent != aux[3] && ent != aux[4];
            }

            //when to make laser visible
            if (laser1 != null)
            {

                if (Thelper.isEntPlayingAnim(laser1, "mm_beam_animation", "start"))
                {

                    float animTime = Thelper.getEntAnimCurrentTime(laser1, "mm_beam_animation", "start");

                    if (animTime > 0.01f)
                    {

                        if (Thelper.isEntPlayingAnim(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip"))
                        {

                            float animTimePlayer = Thelper.getEntAnimCurrentTime(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip");

                            if (animTimePlayer < 0.25)
                                return;

                        }

                        if (laser1.Opacity != 255)
                            laser1.Opacity = 255;

                    }
                    if (animTime > 0.9f)
                    {

                        Thelper.setEntAnimSpeed(laser1, "mm_beam_animation", "start", 0f);

                        Thelper.setEntityAnimCurrentTime(laser1, "mm_beam_animation", "start", 0.9f);

                    }

                }

            }

            if (laser2 != null)
            {

                if (Thelper.isEntPlayingAnim(laser2, "mm_beam_animation", "start"))
                {

                    float animTime = Thelper.getEntAnimCurrentTime(laser2, "mm_beam_animation", "start");

                    if (animTime > 0.01f)
                    {

                        if (laser2.Opacity != 255)
                            laser2.Opacity = 255;

                    }

                    if (animTime > 0.9f)
                    {

                        Thelper.setEntAnimSpeed(laser2, "mm_beam_animation", "start", 0f);

                        Thelper.setEntityAnimCurrentTime(laser2, "mm_beam_animation", "start", 0.9f);

                    }

                }

            }

            laserAttachments("tick");// process laser attachments and rotation

            laserMovementHandler();//   controls user movements

            if (laser1 != null && laser2 != null)
            {

                if (laser1.Opacity == 255 && laser2.Opacity == 255 && !isLaserEnding())
                    laserParticles("spawn");
                else if (isLaserEnding())
                    laserParticles("remove");

                if (laserSoundsOnce[0] && laser1.Opacity == 255)
                {

                    playLaserSound("start");
                    laserSoundsOnce[0] = false;

                }

                bool laserLoopSoundConditions = Thelper.getMSPassed(laserLoopStartTimer) > 1000 && laserSoundsOnce[1] && !laserSoundsOnce[0] && Thelper.isKeyPressed(Keys.R)
                    && laser1.Opacity == 255 && laser2.Opacity == 255 && laser1.IsAttached() && laser2.IsAttached();

                if (laserLoopSoundConditions)
                {

                    playLaserSound("loop");

                    laserSoundsOnce[1] = false;

                }

                if (laser1.Opacity == 255 && laser2.Opacity == 255 && GameplayCamera.IsShaking == false && applyCamShakeOnce == false && !isLaserEnding())
                    Thelper.shakeCamera(CameraShake.SkyDiving, 0.5f);

                else if (isLaserEnding())
                    GameplayCamera.StopShaking();

            }

            //  create a raycast from laser start and end pos, will handle what happens when laser hits any entity. 
            if (aux[2] != null && laser1 != null && laser1.Opacity == 255)
            {

                if (Thelper.isEntPlayingAnim(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip"))
                {

                    float animTime = Thelper.getEntAnimCurrentTime(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip");

                    if (animTime < 0.3)
                        return;

                }

                if (Thelper.getNumberOfFiresInRange(Main.martianManhunter.Position, 40f) > 90f)
                    removeScriptFireInRange();


                Vector3 rayStartPos = aux[2].Position;

                Vector3 rayEndPos = rayStartPos + Thelper.RotationToDirection(aux[2].Rotation).Normalized * 1000f;

                RaycastResult ray = Thelper.Raycast(rayStartPos, rayEndPos, IntersectFlags.Everything, aux[2]);

                if (ray.DidHit)
                {

                    foreach (Ped ped in World.GetNearbyPeds(ray.HitPosition, 10f))
                    {

                        if (ped != Main.martianManhunter)
                            ped.IsExplosionProof = true;

                    }

                    if (visionType == VisionType.Blast)
                    {

                        foreach (Vehicle veh in World.GetNearbyVehicles(ray.HitPosition, 5f))
                        {
                            if (veh.Health > 5)
                            {

                                veh.IsBulletProof = true;

                                veh.IsExplosionProof = true;

                            }
                            else
                            {
                                veh.IsBulletProof = false;

                                veh.IsExplosionProof = false;
                            }


                        }

                    }

                    if (!isLaserEnding())
                    {

                        /* 
                         
                            Seems to be that damage parameter correlates to force being applied, rather than weaponhash
                            maybe damage just has to be above really low value like 1, maybe weaponhash has an effect after that?
                         
                         */

                        int damage = 1;
                        WeaponHash weaponHash = WeaponHash.Pistol;

                        if (visionType == VisionType.Disintegrate)
                        {

                            damage = 100;
                            weaponHash = WeaponHash.HeavySniperMk2;

                        }

                        if (visionType == VisionType.Blast)
                        {

                            damage = 50;
                            weaponHash = WeaponHash.HeavySniperMk2;

                        }

                        if (visionType == VisionType.slice)
                        {

                            damage = 1;
                            weaponHash = WeaponHash.APPistol;

                        }

                        Thelper.shootBullet(rayStartPos, rayEndPos, damage, weaponHash, Main.martianManhunter, true, true, true, -1, true, false, aux[2]);

                        int fireId = 0;

                        if (ray.HitEntity == null)
                            fireId = Thelper.startFireAtPos(ray.HitPosition, 0, false);

                        if (!fireIDs.Contains(fireId) && fireId != 0)
                            fireIDs.Add(fireId);

                        if (!posOnFire.ContainsKey(fireId) && fireId != 0)
                            posOnFire.Add(fireId, Thelper.logTime());

                        if (!Thelper.doesPTFXLoopedExist(ref sparkParticles[0]))
                        {

                            sparkParticles[0] = Thelper.ptfxLoopedCoord("zefgtav_mm_sparks", "sparks", ray.HitPosition, Thelper.Zero, 2f);
                            sparkParticleTimer = Thelper.logTime();

                        }
                        else
                            Thelper.setPTFXLoopedOffsets(ref sparkParticles[0], ray.HitPosition, Thelper.Zero);

                        if (Thelper.getMSPassed(sparkParticleTimer) > 50 && !Thelper.doesPTFXLoopedExist(ref sparkParticles[1]))
                            sparkParticles[1] = Thelper.ptfxLoopedCoord("zefgtav_mm_sparks", "sparks", ray.HitPosition, Thelper.Zero, 2f);
                        else if (Thelper.doesPTFXLoopedExist(ref sparkParticles[1]))
                            Thelper.setPTFXLoopedOffsets(ref sparkParticles[1], ray.HitPosition, Thelper.Zero);


                        Thelper.createExplosion(ray.HitPosition, ExplosionType.Bullet, 1f, 0f, Main.martianManhunter, false, false);

                        Thelper.ptfxNonLoopedCoord("zefgtav_mm_laser", "exp_air_molotov_lod", ray.HitPosition, Thelper.Zero, 0.25f);

                        Thelper.ptfxNonLoopedCoord("zefgtav_mm_laser", "spears", ray.HitPosition, Thelper.Zero, 0.25f);



                    }

                    //Thelper.ptfxNonLoopedCoord("core", "exp_air_molotov_lod", ray.HitPosition, Thelper.Zero, 0.76f);

                    if (applyCamShakeOnce)
                    {

                        Thelper.shakeCamera(CameraShake.Jolt, 0.2f);

                        applyCamShakeOnce = false;

                    }

                    if (ray.HitEntity != null && ray.HitEntity != Main.martianManhunter && hitEntNotAux(ray.HitEntity))
                    {

                        if (Thelper.isEntAPed(ray.HitEntity))
                        {
                            if (visionType == VisionType.Blast)
                            {
                                if (ray.HitEntity.IsOnFire == false)
                                {
                                    if (!((Ped)ray.HitEntity).IsRagdoll)
                                    {
                                        Thelper.makePedRagdollable(((Ped)ray.HitEntity));
                                        Thelper.setPedToRagdollSpecific(((Ped)ray.HitEntity), 800, Thelper.ragdollType.normal_ragdoll);
                                        Thelper.disablePedPainAudio(((Ped)ray.HitEntity), false);
                                        Thelper.playPedPain((Ped)ray.HitEntity, Thelper.painType.fire);
                                    }

                                    Thelper.applyFireToEntity(ray.HitEntity);

                                    if (!entsOnFire.ContainsKey(ray.HitEntity))
                                        entsOnFire.Add(ray.HitEntity, Thelper.logTime());

                                }
                            }
                            else if (visionType == VisionType.Disintegrate)
                            {
                                Ped ped = convertPedToSkeleton((Ped)ray.HitEntity);

                                if (ped != null)
                                {

                                    if (ped.IsOnFire == false)
                                    {

                                        Thelper.applyFireToEntity(ped);

                                        if (!entsOnFire.ContainsKey(ped))
                                            entsOnFire.Add(ped, Thelper.logTime());
                                    }

                                    ped = null;

                                }

                            }
                            else if (visionType == VisionType.slice)
                            {
                                Ped ped = (Ped)ray.HitEntity;

                                Bone lastDamaged = (Bone)ped.Bones.LastDamaged.Index;

                                List<Bone> headBones = new List<Bone>
                                {
                                    Bone.SkelHead,
                                    Bone.SkelNeck1,
                                    Bone.SkelNeck2,
                                };

                                List<Bone> bodyBones = new List<Bone>
                                {
                                    Bone.SkelRoot,
                                    Bone.SkelSpineRoot,
                                    Bone.SkelSpine0,
                                };

                                if (headBones.Contains(lastDamaged))
                                {

                                }
                                else if (bodyBones.Contains(lastDamaged))
                                {
                                    if (!Thelper.isPedDismembered(ped))
                                    {

                                        Thelper.playSoundNAudio(ref Sounds.laserDismember, ref Sounds.fc, ref Sounds.f, 1f);

                                        Ped clonePed = Thelper.upperBodyDismemberment(ped, [Thelper.Zero, Thelper.Zero], CameraShake.SmallExplosion, 0f, 250);

                                        clonePed.IsExplosionProof = true;

                                        clonePed.MarkAsNoLongerNeeded();

                                    }

                                }

                            }


                        }

                        else if (Thelper.isEntAVehicle(ray.HitEntity))
                        {

                            Vehicle veh = (Vehicle)ray.HitEntity;

                            Vector3 vector3 = (veh.Position + (Vector3.Normalize(Game.Player.Character.Position - veh.Position) * 2f));
                            vector3 = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS, veh, vector3.X, vector3.Y, vector3.Z + -0.5f);
                            Function.Call(Hash.SET_VEHICLE_DAMAGE, veh, vector3.X, vector3.Y, vector3.Z, 100.0f, 100.0f, true);
                            veh.Health -= 10;

                            if (veh.Windows.AllWindowsIntact)
                                Thelper.smashVehicleWindow(veh, Thelper.windowType.MiddleLeftWindow, true);
                            if (visionType == VisionType.Blast)
                            {

                                if (veh.Health <= 5)
                                {

                                    veh.IsFireProof = false;

                                    veh.IsExplosionProof = false;

                                    if (veh.IsOnFire == false)
                                        veh.Explode();

                                }

                            }
                            else
                            {

                                if (veh.IsOnFire == false)
                                    veh.Explode();

                            }


                            veh.MarkAsNoLongerNeeded();

                            veh = null;

                        }

                    }

                }

            }

            //  stops an entity on fire after 8 seconds, all fire disappears if too many peds on fire so we have to manually control it.
            foreach (KeyValuePair<Entity, int> entsFire in entsOnFire)
            {

                if (Thelper.getMSPassed(entsFire.Value) > 8000)
                {

                    if (entsFire.Key.IsOnFire)
                        Thelper.stopFireForEntity(entsFire.Key);

                    Thelper.addToEntList(ref entsToRemove, entsFire.Key);
                }

            }

            //  same with fire on ground
            foreach (KeyValuePair<int, int> posFire in posOnFire)
            {

                if (Thelper.getMSPassed(posFire.Value) > 3700)
                {

                    removeID = posFire.Key;

                    Thelper.removeScriptFire(ref removeID);

                    if (!idToRemove.Contains(posFire.Key))
                        idToRemove.Add(posFire.Key);

                }

            }

            for (int i = entsToRemove.Count - 1; i >= 0; i--)
            {

                if (entsToRemove[i] != null)
                {

                    if (entsOnFire.ContainsKey(entsToRemove[i]))
                        entsOnFire.Remove(entsToRemove[i]);

                    entsToRemove[i].MarkAsNoLongerNeeded();
                    entsToRemove.RemoveAt(i);

                }

            }

            for (int i = idToRemove.Count - 1; i >= 0; i--)
            {

                if (idToRemove[i] != 0)
                {

                    if (posOnFire.ContainsKey(idToRemove[i]))
                        posOnFire.Remove(idToRemove[i]);

                    if (fireIDs.Contains(idToRemove[i]))
                        fireIDs.Remove(idToRemove[i]);

                    idToRemove.RemoveAt(i);

                }

            }

        }

        //particle for burn body at pos
        void burnBody(Vector3 pos)
        {

            if (pos != Thelper.Zero)
            {
                float scale = 2f;

                Thelper.ptfxNonLoopedCoord("core", "ent_amb_foundry_steam_spawn", pos + new Vector3(0f, 0f, 0.3f), Thelper.Zero, scale);
                Thelper.ptfxNonLoopedCoord("core", "ent_amb_foundry_steam_spawn", pos, Thelper.Zero, scale);
                Thelper.ptfxNonLoopedCoord("core", "ent_amb_foundry_steam_spawn", pos + new Vector3(0f, 0f, 0.5f), Thelper.Zero, scale);
                Thelper.ptfxNonLoopedCoord("core", "ent_amb_foundry_steam_spawn", pos + new Vector3(0f, 0f, -0.3f), Thelper.Zero, scale);
            }

        }

        void burnBody(Ped ped)
        {

            if (ped != null)
            {
                float scale = 2f;
                Thelper.ptfx_non_looped_entity("core", "ent_amb_foundry_steam_spawn", ped, 0f, 0f, 0.3f, 0f, 0f, 0f, scale, false, false, false);
                Thelper.ptfx_non_looped_entity("core", "ent_amb_foundry_steam_spawn", ped, 0f, 0f, 0f, 0f, 0f, 0f, scale, false, false, false);
                Thelper.ptfx_non_looped_entity("core", "ent_amb_foundry_steam_spawn", ped, 0f, 0f, 0.5f, 0f, 0f, 0f, scale, false, false, false);
                Thelper.ptfx_non_looped_entity("core", "ent_amb_foundry_steam_spawn", ped, 0f, 0f, -0.3f, 0f, 0f, 0f, scale, false, false, false);
            }

        }

        //  converts ped into a skeleton
        Ped convertPedToSkeleton(Ped ped, bool applyForce = true)
        {

            if (ped != null)
            {

                if (ped.Model == "skeleton")
                    return null;

                bool isOnFire = ped.IsOnFire;

                ped.Opacity = 0;



                Vector3 tempPos = ped.Position;
                Vector3 tempRot = ped.Rotation;


                ped.Task.ClearAllImmediately();

                Vector3 tempPosNoRagdoll = ped.Position;

                float pedMaxHealth = ped.MaxHealthFloat;
                float pedHealth = ped.HealthFloat;

                Ped newPed = Thelper.spawnPed("skeleton", tempPos.Around(5), tempRot.ToHeading());
                speedBurstParticle(ped);
                if (newPed != null)
                {

                    burnBody(tempPosNoRagdoll);

                    newPed.Opacity = 0;

                    newPed.IsExplosionProof = true;

                    newPed.MaxHealthFloat = pedMaxHealth;
                    newPed.HealthFloat = pedHealth;

                    Thelper.setCollisionBetweenEntities(ped, newPed, false);

                    newPed.PositionNoOffset = tempPos;
                    newPed.Rotation = tempRot;

                    Thelper.makePedRagdollable(newPed);
                    Thelper.setPedToRagdollSpecific(newPed, 800, Thelper.ragdollType.normal_ragdoll);

                    if (isOnFire)
                    {

                        Thelper.applyFireToEntity(newPed);

                        if (!entsOnFire.ContainsKey(newPed))
                            entsOnFire.Add(newPed, Thelper.logTime());

                    }

                    burnBody(newPed);
                    speedBurstParticle(newPed);
                    newPed.Opacity = 255;

                    if (aux[2] != null && laser1 != null && laser1.Opacity == 255 && !isLaserEnding())
                    {

                        Vector3 rayStartPos = aux[2].Position;

                        Vector3 targetPos = newPed.Position;

                        Vector3 dir = (targetPos - rayStartPos).Normalized * 3f;

                        newPed.ApplyForce(dir);

                    }

                    ped.MarkAsNoLongerNeeded();

                    return newPed;

                }

                return null;

            }

            return null;

        }

        //  checks if laser is active
        public static bool isLaserActive()
        {

            for (int i = 0; i < aux.Length; i++)
            {

                if (aux[i] == null || !aux[i].Exists())
                    return false;

            }

            if (isLaserEnding())
                return false;

            if (laser1 == null || !laser1.Exists())
                return false;

            if (laser2 == null || !laser2.Exists())
                return false;

            return true;

        }

        //  checks if aux is attached properly and in the game
        bool isAuxIntact()
        {

            if (aux[0] != null && aux[1] != null && aux[2] != null && aux[3] != null && aux[4] != null)
            {

                if (aux[0].IsAttached() && aux[1].IsAttached() && aux[3].IsAttached() && aux[4].IsAttached())
                    return true;

            }

            return false;

        }

        //delete the laser, ragdoll which deletes fast, natural with naturally stops it
        public static void deleteLaser(string type)
        {

            if (type == "ragdoll")
            {

                endLaserOnce = false;

                GameplayCamera.StopShaking();

                Thelper.STOP_ANIM_TASK(Main.martianManhunter, "zefgtav_mm_laser@animations", "idle_no_fist_clip", 0.8f);

                if (laser1 != null)
                {

                    stopLaserAnimation(laser1, "end", 8f);

                    laser1.Opacity = 0;

                }

                if (laser2 != null)
                {

                    stopLaserAnimation(laser2, "end", 8f);

                    laser2.Opacity = 0;

                }

                laserParticles("remove");

                stopLaserSound("start");
                stopLaserSound("loop");
                stopLaserSound("end");

                if (laser1 != null)
                {

                    if (laser1.IsAttached())
                        laser1.Detach();

                    laser1.Delete();

                    laser1.MarkAsNoLongerNeeded();

                    laser1 = null;

                }

                if (laser2 != null)
                {

                    if (laser2.IsAttached())
                        laser2.Detach();

                    laser2.Delete();

                    laser2.MarkAsNoLongerNeeded();

                    laser2 = null;

                }

                /* for (int i = 0; i < aux.Length; i++)
                 {

                     if (aux[i] != null)
                     {

                         if (aux[i].IsAttached())
                             aux[i].Detach();

                         aux[i].IsCollisionEnabled = false;

                         aux[i].Delete();

                         aux[i].MarkAsNoLongerNeeded();

                         aux[i] = null; 

                     }

                 }*/

                if (Thelper.doesPTFXLoopedExist(ref sizzleParticle[0]))
                    Thelper.removeLoopedPTFX(ref sizzleParticle[0]);

                if (Thelper.doesPTFXLoopedExist(ref sizzleParticle[1]))
                    Thelper.removeLoopedPTFX(ref sizzleParticle[1]);

            }
            else if (type == "natural")
            {

                GameplayCamera.StopShaking();

                Thelper.STOP_ANIM_TASK(Main.martianManhunter, "zefgtav_mm_laser@animations", "idle_no_fist_clip", 1f);

                stopLaserSound("start");
                stopLaserSound("loop");
                stopLaserSound("end");

                playLaserAnimation(laser1, "end");
                playLaserAnimation(laser2, "end");

                deleteRot[0] = smoothedRotationLeft;
                deleteRot[1] = smoothedRotationRight;

                laserEndTimer = Thelper.logTime();

                endLaserOnce = true;

            }

        }

        //  checks if laser end anim is playing
        public static bool isLaserEnding()
        {

            if (laser1 != null)
                return Thelper.IS_ENTITY_PLAYING_ANIM(laser1, "mm_beam_animation", "end");

            return false;
        }

        //  play a laser sound: start, loop, or end
        void playLaserSound(string type, float volume = 0.3f)
        {

            switch (type)
            {

                case "start":
                    Thelper.playSoundNAudio(ref Sounds.laserStart, ref Sounds.cc, ref Sounds.c, volume);
                    break;

                case "loop":
                    Thelper.playSoundNAudio(ref Sounds.laserLoop, ref Sounds.dc, ref Sounds.d, volume);
                    break;

                case "end":
                    //Thelper.playSoundNAudio(ref Sounds.laserEnd, ref Sounds.ec, ref Sounds.e, volume);
                    break;

            }

        }

        //  stop a laser sound: start, loop, or end
        public static void stopLaserSound(string type)
        {

            switch (type)
            {

                case "start":
                    Sounds.laserStart.Stop();
                    break;

                case "loop":
                    Sounds.laserLoop.Stop();
                    break;

                case "end":
                    //  Sounds.laserEnd.Stop();
                    break;

            }

        }

        //  disable base game engine keys
        void disabledKeys()
        {

            Game.DisableControlThisFrame(GTA.Control.Attack);
            Game.DisableControlThisFrame(GTA.Control.Attack2);
            Game.DisableControlThisFrame(GTA.Control.MeleeAttack1);
            Game.DisableControlThisFrame(GTA.Control.MeleeAttack2);
            Game.DisableControlThisFrame(GTA.Control.MeleeAttackAlternate);
            Game.DisableControlThisFrame(GTA.Control.MeleeAttackHeavy);
            Game.DisableControlThisFrame(GTA.Control.MeleeAttackLight);

        }

        //  handle movement during laser
        void laserMovementHandler()
        {

            if (laser1 != null && laser2 != null)
            {

                if (Game.IsControlPressed(GTA.Control.MoveUpOnly))
                    Thelper.STOP_ANIM_TASK(Main.martianManhunter, "zefgtav_mm_laser@animations", "idle_no_fist_clip", 1.5f);


                else if (Game.Player.Character.Speed < 1 && isLaserEnding() == false && isLaserActive() && !flight.flightMode)
                {

                    if (!Thelper.IS_ENTITY_PLAYING_ANIM(Main.martianManhunter, "zefgtav_mm_laser@animations", "idle_no_fist_clip"))
                        Thelper.playAnimControlTransition(Main.martianManhunter, "zefgtav_mm_laser@animations", "idle_no_fist_clip", 2f, 4, 1);



                }

            }

            bool flightIdleLaserStartAnim = Thelper.isEntPlayingAnim(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip");
            bool flightIdleLaserHoldAAnim = Thelper.isEntPlayingAnim(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_laser_hold_a_clip");
            bool flightIdleLaserHoldBAnim = Thelper.isEntPlayingAnim(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_laser_hold_b_clip");

            if (flightIdleLaserStartAnim)
            {
                float animTime = Thelper.getEntAnimCurrentTime(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip");

                if (animTime > 0.4f)
                {

                    Thelper.STOP_ANIM_TASK(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_start_laser_clip", 2f);

                    Thelper.playAnimControlTransition(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_laser_hold_a_clip", 8f, 2f, 1);

                }

            }

            //  This stops start anim from being played 
            else if (!flightIdleLaserHoldAAnim && !flightIdleLaserStartAnim && flight.flightMode && Thelper.isKeyPressed(Keys.R) && isLaserActive()
                && !Thelper.IS_ENTITY_PLAYING_ANIM(Main.martianManhunter, "zefgtav_mm_flight@animations", "flight_idle_b_f_clip"))
                Thelper.playAnimControlTransition(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_laser_hold_a_clip", 3f, 2f, 1);

            //  Thelper.playAnimControlTransition(Main.martianManhunter, "zefgtav_mm_laser@animations", "flight_idle_laser_hold_b_clip", 3f, 2f, 1);

        }

        //  disintegrate ped particle
        public static void speedBurstParticle(Ped ped)
        {
            if (ped != null)
            {
                Vector3 pos = ped.Position;
                Thelper.ptfx_non_looped_coord("core", "blood_entry_shotgun", pos.X, pos.Y, pos.Z + -0.3f, -180f, 0f, 0f, 4f, false, false, false);
                Thelper.ptfx_non_looped_coord("core", "blood_entry_shotgun", pos.X, pos.Y, pos.Z, 0f, 0f, 0f, 4f, false, false, false);
                Thelper.ptfx_non_looped_coord("core", "blood_entry_sniper", pos.X, pos.Y, pos.Z, 0f, 0f, 0f, 4f, false, false, false);
                Thelper.ptfx_non_looped_coord("core", "blood_headshot", pos.X, pos.Y, pos.Z, -180f, 0f, 0f, 2f, false, false, false);

                Thelper.ptfx_non_looped_coord("core", "blood_mist", pos.X, pos.Y, pos.Z, 0f, 0f, 0f, 2f, false, false, false);
                Thelper.ptfx_non_looped_coord("zefgtav_dr_doom_bomb", "blood_splatter", pos.X, pos.Y, pos.Z, 0f, 0f, 0f, 2.5f, false, false, false);

                Thelper.ptfx_non_looped_coord("core", "bang_blood", pos.X, pos.Y, pos.Z, 0f, 0f, 0f, 5f, false, false, false);
            }

        }

        private void MartianVision_Tick(object sender, EventArgs e)
        {

            
            if (Main.powersOn)
            {

                disabledKeys();


                if (Thelper.isKeyPressed(Keys.R))
                    spawnLaser();

                if (Thelper.isKeyJustReleased(Keys.R))
                {

                    if (Main.martianManhunter.Speed < 4)
                        deleteLaser("natural");
                    else
                        deleteLaser("ragdoll");
                }



                if (Thelper.getMSPassed(laserEndTimer) > 1800 && endLaserOnce)
                {
                    deleteLaser("ragdoll");
                    endLaserOnce = false;
                }




                laserTick();

            }

        }

    }

}
