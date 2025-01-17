using System;
using System.Collections.Generic;
using UnityEngine;
namespace SSTUTools
{

    /// <summary>
    /// Generic animation module intended to be controlled by other PartModules.
    /// <para>Does not include any GUI or direct-user-interactivity; all state changes
    /// must be initiated from external sources</para>
    /// </summary>
    public class SSTUAnimateControlled : PartModule
    {
        //lookup value for external modules to find the proper animation module when multiple anim modules are in use
        [KSPField]
        public int animationID = 0;

        [KSPField]
        public float animationSpeed = 1;

        [KSPField]
        public int animationLayer = 1;

        [KSPField]
        public String animationName;

        [KSPField(isPersistant = true)]
        public String persistentState = AnimState.STOPPED_START.ToString();

        private AnimState currentAnimState = AnimState.STOPPED_START;

        private List<Action<AnimState>> onAnimStateChangeCallbacks = new List<Action<AnimState>>();

        private SSTUAnimData[] animationData;

        //Static method for use by other modules to locate a control module; reduces code duplication in animation controlling modules
        public static SSTUAnimateControlled locateAnimationController(Part part, int id, Action<AnimState> callback)
        {
            if (id < 0)
            {
                return null;
            }
            SSTUAnimateControlled[] potentialAnimators = part.GetComponents<SSTUAnimateControlled>();
            foreach (SSTUAnimateControlled ac in potentialAnimators)
            {
                if (ac.animationID == id)
                {
                    ac.addCallback(callback);
                    return ac;
                }
            }
            return null;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
            {
                return;//should never happen, onStart not called for prefabs
            }
            if (animationData == null) { initialize(); }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            try
            {
                currentAnimState = (AnimState)Enum.Parse(typeof(AnimState), persistentState);
            }
            catch (Exception e)
            {
                currentAnimState = AnimState.STOPPED_START;
                persistentState = currentAnimState.ToString();
                print(e.Message);
            }
            initialize();
        }

        private void initialize()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);
            ConfigNode[] animNodes = node.GetNodes("ANIMATION");
            int len = animNodes.Length;
            int ex = 0;
            if (animationName != null)
            {
                ex = 1;
            }
            animationData = new SSTUAnimData[len+ex];
            for (int i = 0; i < len; i++)
            {
                animationData[i] = new SSTUAnimData(animNodes[i], part);
            }
            if (ex > 0)
            {
                ConfigNode legacyNode = new ConfigNode("ANIMATION");
                legacyNode.AddValue("name", animationName);
                legacyNode.AddValue("speed", animationSpeed);
                legacyNode.AddValue("layer", animationLayer);
                animationData[len] = new SSTUAnimData(legacyNode, part);
            }
            restorePreviousAnimationState(currentAnimState);
        }

        public void reInitialize()
        {
            if (animationData != null)
            {
                animationData = null;
                initialize();
            }
        }

        public bool initialized() { return animationData != null; }

        public void addCallback(Action<AnimState> cb)
        {
            onAnimStateChangeCallbacks.Add(cb);
        }

        //External method to set the state; does not callback on this state change, as this is supposed to originate -from- the callback;
        //it should be aware of its own instigated state changes
        public void setToState(AnimState newState)
        {
            setAnimState(newState, false);
        }

        public AnimState getAnimationState()
        {
            return currentAnimState;
        }

        public void Update()
        {
            if (currentAnimState == AnimState.PLAYING_BACKWARD || currentAnimState == AnimState.PLAYING_FORWARD)
            {
                bool playing = false;
                int len = animationData.Length;
                for (int i = 0; i < len && !playing; i++)
                {
                    int animLen = animationData[i].anims.Length;
                    for (int k = 0; k < animLen && !playing; k++)
                    {
                        if (animationData[i].anims[k][animationData[i].animationName].enabled)
                        {
                            playing = true;
                        }
                    }
                }
                //if no longer playing, set the new animation state and inform the callback of the change
                if (!playing)
                {
                    AnimState newState = currentAnimState == AnimState.PLAYING_BACKWARD ? AnimState.STOPPED_START : AnimState.STOPPED_END;
                    setAnimState(newState, true);
                }
            }
        }

        private void setAnimState(AnimState newState, bool callback)
        {
            switch (newState)
            {
                case AnimState.PLAYING_BACKWARD:
                    {
                        setAnimSpeed(-1f);
                        if (currentAnimState == AnimState.STOPPED_END)//enforce play backwards from end
                        {
                            setAnimTime(1f);
                        }
                        playAnimation();
                        break;
                    }
                case AnimState.PLAYING_FORWARD:
                    {
                        setAnimSpeed(1f);
                        if (currentAnimState == AnimState.STOPPED_START)//enforce play forwards from beginning
                        {
                            setAnimTime(0f);
                        }
                        playAnimation();
                        break;
                    }
                case AnimState.STOPPED_END:
                    {
                        setAnimTime(1);
                        setAnimSpeed(1);
                        playAnimation();
                        break;
                    }
                case AnimState.STOPPED_START:
                    {
                        setAnimTime(0);
                        setAnimSpeed(-1);
                        playAnimation();
                        break;
                    }
            }
            currentAnimState = newState;
            persistentState = currentAnimState.ToString();
            if (callback && onAnimStateChangeCallbacks != null)
            {
                int len = onAnimStateChangeCallbacks.Count;
                for (int i = 0; i < len; i++)
                {
                    onAnimStateChangeCallbacks[i].Invoke(currentAnimState);
                }
            }
        }

        private void playAnimation()
        {
            int len = animationData.Length;
            for (int i = 0; i < len; i++)
            {
                animationData[i].playAnimation();
            }
        }

        private void stopAnimation()
        {
            int len = animationData.Length;
            for (int i = 0; i < len; i++)
            {
                animationData[i].stopAnimation();
            }
        }

        private void setAnimTime(float time)
        {
            int len = animationData.Length;
            for (int i = 0; i < len; i++)
            {
                animationData[i].setAnimTime(time);
            }
        }

        private void setAnimSpeed(float speed)
        {
            int len = animationData.Length;
            for (int i = 0; i < len; i++)
            {
                animationData[i].setAnimSpeed(speed);
            }
        }

        private void restorePreviousAnimationState(AnimState state)
        {
            if (state == AnimState.PLAYING_BACKWARD)
            {
                state = AnimState.STOPPED_START;
            }
            else if (state == AnimState.PLAYING_FORWARD)
            {
                state = AnimState.STOPPED_END;
            }
            setAnimState(state, false);
        }
    }

    public class SSTUAnimData
    {
        public readonly Animation[] anims;
        public readonly String animationName;
        public readonly float animationSpeed = 1;
        public readonly int animationLayer = 1;
        
        public SSTUAnimData(ConfigNode node, Part part)
        {
            animationName = node.GetStringValue("name");
            animationSpeed = node.GetFloatValue("speed", animationSpeed);
            animationLayer = node.GetIntValue("layer", animationLayer);
            Animation[] animsBase = part.gameObject.GetComponentsInChildren<Animation>(true);
            List<Animation> al = new List<Animation>();
            foreach (Animation a in animsBase)
            {
                AnimationClip c = a.GetClip(animationName);
                if (c != null)
                {
                    al.Add(a);
                }
            }
            anims = al.ToArray();
            if (anims == null || anims.Length == 0)
            {
                MonoBehaviour.print("ERROR: No animations found for animation name: " + animationName);
            }
            foreach (Animation a in anims)
            {
                a[animationName].layer = animationLayer;
                a[animationName].wrapMode = WrapMode.Once;
                a.wrapMode = WrapMode.Once;
            }
        }

        public void playAnimation()
        {
            foreach (Animation a in anims)
            {
                a.Play(animationName);
            }
        }

        public void stopAnimation()
        {
            foreach (Animation a in anims)
            {
                a[animationName].speed = 0f;
                a.Stop(animationName);
            }
        }

        public void setAnimTime(float time)
        {
            foreach (Animation a in anims)
            {
                a[animationName].normalizedTime = time;
            }
        }

        public void setAnimSpeed(float speed)
        {
            foreach (Animation a in anims)
            {
                a[animationName].speed = speed * animationSpeed;
            }
        }
    }
}

