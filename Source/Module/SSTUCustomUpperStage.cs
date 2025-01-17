﻿using System;
using System.Collections;
using UnityEngine;

namespace SSTUTools
{
    public class SSTUCustomUpperStage : PartModule, IPartCostModifier, IPartMassModifier
    {

        #region ----------------- REGION - Standard KSP-accessible config fields -----------------
        /// <summary>
        /// quick/dirty/easy flag to determine if should even attempt to load/manipulate split-tank elements
        /// </summary>
        [KSPField]
        public bool splitTank = true;

        /// <summary>
        /// How much is the 'height' incremented for every 'large' tank height step? - this value is further scaled based on the currently selected tank diameter
        /// </summary>
        [KSPField]
        public float tankHeightIncrement = 0.5f;

        /// <summary>
        /// How much is the 'diameter' incremented for every 'large' tank diameter step? - this value is -not- scaled, and used as-is.
        /// </summary>
        [KSPField]
        public float tankDiameterIncrement = 1.25f;

        /// <summary>
        /// Minimum tank height that can be set through VAB; this is just the adjustable tank portion and does not include any cap height
        /// </summary>
        [KSPField]
        public float minTankHeight = 1;

        /// <summary>
        /// Maximum tank height that may be set through the VAB; this is just the adjustable portion and does not include any cap height
        /// </summary>
        [KSPField]
        public float maxTankHeight = 5;

        /// <summary>
        /// Minimum tank diameter that may be set through the VAB
        /// </summary>
        [KSPField]
        public float minTankDiameter = 1.25f;

        /// <summary>
        /// Maximum tank diameter that may be set through the VAB
        /// </summary>
        [KSPField]
        public float maxTankDiameter = 10;

        /// <summary>
        /// Determines the diameter of the upper stage part.  Used to re-scale the input model to this diameter
        /// </summary>
        [KSPField]
        public float defaultTankDiameter = 2.5f;

        /// <summary>
        /// Default 'height' of the adjustable tank portion.
        /// </summary>
        [KSPField]
        public float defaultTankHeight = 1f;

        /// <summary>
        /// The default mount model to use when the upper stage is first initialized/pulled out of the editor; can be further adjusted by user in editor
        /// Mandatory field, -must- be populated by a valid mount option for this part.
        /// </summary>
        [KSPField]
        public String defaultMount = String.Empty;

        /// <summary>
        /// The default name entry for the intertank structure definition
        /// </summary>
        [KSPField]
        public String defaultIntertank = String.Empty;

        /// <summary>
        /// The thrust output of the RCS system at the default tank diameter; scaled using square-scaling methods to determine 
        /// </summary>
        [KSPField]
        public float defaultRcsThrust = 1;

        [KSPField]
        public int topFairingIndex = 0;

        [KSPField]
        public int lowerFairingIndex = 1;

        [KSPField]
        public String interstageNodeName = "interstage";

        [KSPField]
        public String baseTransformName = "SSTUCustomUpperStageBaseTransform";

        [KSPField]
        public String rcsTransformName = "SSTUMUSRCS";

        [KSPField]
        public String techLimitSet = "Default";

        [KSPField]
        public bool subtractMass = false;

        [KSPField]
        public bool subtractCost = false;

        #endregion

        #region ----------------- REGION - GUI visible fields and fine tune adjustment contols - do not edit through config -----------------
        [KSPField(guiName = "Tank Height", guiActive = false, guiActiveEditor = true)]
        public float guiTankHeight;

        [KSPField(guiName = "Total Height", guiActive = false, guiActiveEditor = true)]
        public float guiTotalHeight;

        [KSPField(guiName = "Tank Mass", guiActive = false, guiActiveEditor = true)]
        public float guiDryMass;

        [KSPField(guiName = "RCS Thrust", guiActive = false, guiActiveEditor = true)]
        public float guiRcsThrust;
        #endregion

        #region ----------------- REGION - persistent data fields - do not edit through config ----------------- 
        //  --  NOTE  -- 
        // Below here are non-config-editable fields, used for persistance of the current settings; do not attempt to alter/adjust these through config, or things will -not- go as you expect

        /// <summary>
        /// Current absolute tank diameter (of the upper tank for split-tank, or of the full tank for common-bulkhead types)
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName ="Diameter"),
         UI_FloatEdit(sigFigs = 3, suppressEditorShipModified = true)]
        public float currentTankDiameter = -1f;

        /// <summary>
        /// Current absolute (post-scale) height of the adjustable tank portion
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName = "Height"),
         UI_FloatEdit(sigFigs =3, suppressEditorShipModified = true)]
        public float currentTankHeight = 0f;

        /// <summary>
        /// The currently selected/enabled mount option.
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName ="Mount"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentMount = String.Empty;

        /// <summary>
        /// The currently selected/enabled intertank option (if any).
        /// </summary>
        [KSPField(isPersistant = true, guiActiveEditor =true, guiName ="Intertank"),
         UI_ChooseOption(suppressEditorShipModified = true)]
        public String currentIntertank = String.Empty;

        [KSPField(isPersistant = true)]
        public String currentMountTexture = String.Empty;

        //[KSPField(isPersistant = true)]
        //public String currentCapTextureSet = String.Empty;

        //[KSPField(isPersistant = true)]
        //public String currentIntertankTextureSet = String.Empty;

        //[KSPField(isPersistant = true)]
        //public String currentUpperTankTextureSet = String.Empty;

        //[KSPField(isPersistant = true)]
        //public String currentLowerTankTextureSet = String.Empty;

        /// <summary>
        /// The current RCS thrust; this value will be 'set' into the RCS module (if found/present)
        /// </summary>
        [KSPField(isPersistant = true)]
        public float currentRcsThrust = 0f;

        /// <summary>
        /// Used solely to track if resources have been initialized, as this should only happen once on first part creation (regardless of if it is created in flight or in the editor);
        /// Unsure of any cleaner way to track a simple boolean value across the lifetime of a part, seems like the part-persistence data is probably it...
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool initializedResources = false;

        [KSPField(isPersistant = true)]
        public bool initializedFairing = false;

        #endregion

        #region ----------------- REGION - Private working value fields ----------------- 

        //cached values for editor updating of height/diameter        
        private string prevMount;
        private string prevIntertank;
        private float prevHeight;
        private float prevTankDiameter;
        
        //geometry related values, mostly for updating of fairings        
        private float partTopY;
        private float topFairingBottomY;
        private float partBottomY;
        private float bottomFairingTopY;

        //cached values for updating of part volume and mass
        private float totalTankVolume = 0;
        private float moduleMass = 0;
        private float moduleCost = 0;
        private float rcsThrust = 0;

        // tech limit values are updated every time the part is initialized in the editor; ignored otherwise
        private float techLimitMaxDiameter;

        //Private-instance-local fields for tracking the current/loaded config; basically parsed from configNodeData when config is loaded
        //upper, rcs, and mount must be present for every part
        private SingleModelData upperModule;
        private SingleModelData upperTopCapModule;
        private SingleModelData upperBottomCapModule;
        private SSTUCustomUpperStageRCS rcsModule;
        private MountModelData[] mountModules;
        private MountModelData currentMountModule;
        //lower and intertank need only be present for split-tank type parts
        private SingleModelData lowerModule;
        private SingleModelData lowerBottomCapModule;
        private SingleModelData[] intertankModules;
        private SingleModelData currentIntertankModule;
        

        private bool initialized = false;
        #endregion

        #region ----------------- REGION - GUI Interaction methods ----------------- 

        [KSPEvent(guiName = "Next Mount Texture", guiActive = false, guiActiveEditor = true, guiActiveUnfocused = false)]
        public void nextMountTextureEvent()
        {
            setMountTextureFromEditor(currentMountModule.getNextTextureSetName(currentMountTexture, false), true);
        }

        public void onDiameterUpdated(BaseField field, object obj)
        {
            if (currentTankDiameter != prevTankDiameter)
            {
                setTankDiameterFromEditor(currentTankDiameter, true);
                SSTUModInterop.onPartGeometryUpdate(part, true);
                SSTUStockInterop.fireEditorUpdate();
            }            
        }

        public void onHeightUpdated(BaseField field, object obj)
        {
            if (currentTankHeight != prevHeight)
            {
                updateTankHeightFromEditor(currentTankHeight, true);
                SSTUModInterop.onPartGeometryUpdate(part, true);
                SSTUStockInterop.fireEditorUpdate();
            }
        }

        public void onMountUpdated(BaseField field, object obj)
        {
            updateMountModelFromEditor(currentMount, true);
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        public void onIntertankUpdated(BaseField field, object obj)
        {
            updateIntertankModelFromEditor(currentIntertank, true);
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        /// <summary>
        /// Editor callback method for when tank height changes.  Updates model positions, attach node/attached part positions, 
        /// </summary>
        private void updateTankHeightFromEditor(float newHeight, bool updateSymmetry)
        {
            currentTankHeight = newHeight;
            updateEditorFields();
            updateModules(true);
            updateModels();
            updateTankStats();
            updateContainerVolume();
            updateGuiState();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().updateTankHeightFromEditor(newHeight, false);
                }
            }
        }

        /// <summary>
        /// Updates the current tank diameter from user input.  Subsequently updates internal and GUI variables, and redoes the setup for the part resources
        /// </summary>
        private void setTankDiameterFromEditor(float newDiameter, bool updateSymmetry)
        {     
            currentTankDiameter = newDiameter;
            updateEditorFields();
            updateModules(true);
            updateModels();
            updateTankStats();
            updateContainerVolume();
            updateGuiState();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().setTankDiameterFromEditor(newDiameter, false);
                }
            }
        }

        /// <summary>
        /// Updates the selected mount model from user input
        /// </summary>
        /// <param name="nextDef"></param>
        private void updateMountModelFromEditor(String newMount, bool updateSymmetry)
        {
            removeCurrentModel(currentMountModule);
            currentMountModule = Array.Find(mountModules, m => m.name == newMount);
            currentMount = newMount;
            setupModel(currentMountModule, part.transform.FindRecursive("model").FindOrCreate(baseTransformName), ModelOrientation.BOTTOM);
            updateModules(true);
            updateModels();
            updateFuelVolume();
            updateContainerVolume();
            updateGuiState();
            if (!currentMountModule.isValidTextureSet(currentMountTexture)) { currentMountTexture = currentMountModule.modelDefinition.defaultTextureSet; }
            currentMountModule.enableTextureSet(currentMountTexture);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().updateMountModelFromEditor(newMount, false);
                }
            }
        }

        /// <summary>
        /// Updates the current intertank mesh/model from user input
        /// </summary>
        /// <param name="newDef"></param>
        private void updateIntertankModelFromEditor(String newModel, bool updateSymmetry)
        {
            removeCurrentModel(currentIntertankModule);
            currentIntertankModule = Array.Find(intertankModules, m => m.name == newModel);
            currentIntertank = newModel;
            setupModel(currentIntertankModule, part.transform.FindRecursive("model").FindOrCreate(baseTransformName), ModelOrientation.CENTRAL);
            updateModules(true);
            updateModels();
            updateTankStats();
            updateContainerVolume();
            updateGuiState();
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().updateIntertankModelFromEditor(newModel, false);
                }
            }
        }

        private void setMountTextureFromEditor(String newSet, bool updateSymmetry)
        {
            currentMountTexture = newSet;
            currentMountModule.enableTextureSet(newSet);

            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().setMountTextureFromEditor(newSet, false);
                }
            }
        }

        #endregion

        #region ----------------- REGION - KSP Overrides ----------------- 

        /// <summary>
        /// OnLoad override.  Loads previously saved config data, stores module config node for later reading, and does pre-start initialization
        /// </summary>
        /// <param name="node"></param>
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            initialize();
        }
        
        /// <summary>
        /// OnStart override, does basic startup/init stuff, including building models and registering for editor events
        /// </summary>
        /// <param name="state"></param>
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            initialize();
            Fields["currentTankDiameter"].uiControlEditor.onFieldChanged = onDiameterUpdated;
            Fields["currentTankHeight"].uiControlEditor.onFieldChanged = onHeightUpdated;            
            Fields["currentMount"].uiControlEditor.onFieldChanged = onMountUpdated;
            Fields["currentIntertank"].uiControlEditor.onFieldChanged = onIntertankUpdated;
            float max = techLimitMaxDiameter < maxTankDiameter ? techLimitMaxDiameter : maxTankDiameter;
            this.updateUIFloatEditControl("currentTankDiameter", minTankDiameter, max, tankDiameterIncrement*2, tankDiameterIncrement, tankDiameterIncrement*0.05f, true, currentTankDiameter);
            this.updateUIFloatEditControl("currentTankHeight", minTankHeight, maxTankHeight, tankHeightIncrement*2f, tankHeightIncrement, tankHeightIncrement*0.05f, true, currentTankHeight);
            string[] names = SSTUUtils.getNames(mountModules, m => m.name);
            this.updateUIChooseOptionControl("currentMount", names, names, true, currentMount);
            if (this.splitTank)
            {
                names = SSTUUtils.getNames(intertankModules, m => m.name);
                this.updateUIChooseOptionControl("currentIntertank", names, names, true, currentIntertank);
            }
            else
            {
                Fields["currentIntertank"].guiActiveEditor = false;
            }
            SSTUModInterop.onPartGeometryUpdate(part, true);
            SSTUStockInterop.fireEditorUpdate();
        }

        public override string GetInfo()
        {
            return "This part has configurable diameter, height, bottom-cap, and fairings.";
        }

        /// <summary>
        /// Unity method override, supposedly called after -all- modules have had OnStart() called.
        /// Overriden to update fairing and RCS modules, which might not exist when OnStart() is called for this module
        /// </summary>
        public void Start()
        {
            if (!initializedFairing && HighLogic.LoadedSceneIsEditor)
            {
                initializedFairing = true;
                updateFairing(true);
            }
            if (!initializedResources && HighLogic.LoadedSceneIsEditor)
            {
                initializedResources = true;
                updateContainerVolume();
            }
            updateRCSThrust();
        }

        /// <summary>
        /// Return the current part cost/modifier.  Returns the pre-calculated tank cost.
        /// </summary>
        /// <param name="defaultCost"></param>
        /// <returns></returns>
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            return subtractCost ? -defaultCost + moduleCost : moduleCost;
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
        {
            return subtractMass ? -defaultMass + moduleMass : moduleMass;
        }

        public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
        public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

        #endregion

        #region ----------------- REGION - Initialization Methods ----------------- 

        /// <summary>
        /// Basic initialization code, should only be ran once per part-instance (though, is safe to call from both start and load)
        /// </summary>
        private void initialize()
        {
            if (initialized) { return; }
            initialized = true;
            SSTUUtils.destroyChildren(part.transform.FindRecursive(baseTransformName));
            if (currentTankDiameter==-1f)
            {
                //should only run once, on the prefab part; which is fine, as the models/settings will be cloned and carried over to the editor part
                currentTankDiameter = defaultTankDiameter;
                currentTankHeight = defaultTankHeight;
                currentMount = defaultMount;
                currentIntertank = defaultIntertank;
                currentRcsThrust = defaultRcsThrust;
            }
            loadConfigData();
            TechLimit.updateTechLimits(techLimitSet, out techLimitMaxDiameter);
            if (currentTankDiameter > techLimitMaxDiameter)
            {
                currentTankDiameter = techLimitMaxDiameter;
            }

            updateModules(false);
            buildSavedModel();
            updateModels();
            updateTankStats();
            updateEditorFields();
            updateGuiState();
            updateTextureSet(false);
        }
                
        /// <summary>
        /// Restores the editor-only diameter and height-adjustment values;
        /// </summary>
        private void updateEditorFields()
        {
            prevIntertank = currentIntertank;
            prevMount = currentMount;
            prevTankDiameter = currentTankDiameter;
            prevHeight = currentTankHeight;
        }
        
        /// <summary>
        /// Loads all of the part definitions and values from the stashed config node data
        /// </summary>
        private void loadConfigData()
        {
            ConfigNode node = SSTUStockInterop.getPartModuleConfig(part, this);

            //mandatory nodes, -all- tank types must have these
            ConfigNode tankUpperNode = node.GetNode("TANKUPPER");
            ConfigNode upperTopCapNode = node.GetNode("TANKUPPERTOPCAP");
            ConfigNode upperBottomCapNode = node.GetNode("TANKUPPERBOTTOMCAP");

            ConfigNode rcsNode = node.GetNode("RCS");
            ConfigNode[] mountNodes = node.GetNodes("MOUNT");
            
            upperModule = new SingleModelData(tankUpperNode);
            upperTopCapModule = new SingleModelData(upperTopCapNode);
            upperBottomCapModule = new SingleModelData(upperBottomCapNode);
            rcsModule = new SSTUCustomUpperStageRCS(rcsNode);

            //load mount configs
            int len = mountNodes.Length;
            mountModules = new MountModelData[len];
            for (int i = 0; i < len; i++)
            {
                mountModules[i] = new MountModelData(mountNodes[i]);
            }
            currentMountModule = Array.Find(mountModules, l => l.name == currentMount);
            if (!currentMountModule.isValidTextureSet(currentMountTexture))
            {
                currentMountTexture = currentMountModule.modelDefinition.defaultTextureSet;
            }

            if (splitTank)
            {
                //fields that are only populated by split-tank type upper-stages
                ConfigNode tankLowerNode = node.GetNode("TANKLOWER");
                ConfigNode lowerBottomCapNode = node.GetNode("TANKLOWERBOTTOMCAP");
                ConfigNode[] intertankNodes = node.GetNodes("INTERTANK");
                lowerModule = new SingleModelData(tankLowerNode);
                lowerBottomCapModule = new SingleModelData(lowerBottomCapNode);
                //load intertank configs
                len = intertankNodes.Length;
                intertankModules = new SingleModelData[len];
                for (int i = 0; i < len; i++)
                {
                    intertankModules[i] = new SingleModelData(intertankNodes[i]);
                }
                currentIntertankModule = Array.Find(intertankModules, l => l.name == currentIntertank);
            }
        }

        #endregion

        #region ----------------- REGION - Module Position / Parameter Updating ----------------- 

        /// <summary>
        /// Updates the internal cached scale of each of the modules; applied to models later
        /// </summary>
        private void updateModuleScales()
        {
            float scale = currentTankDiameter / defaultTankDiameter;
            
            upperTopCapModule.updateScaleForDiameter(currentTankDiameter);
            upperModule.updateScaleForHeightAndDiameter(currentTankHeight*scale, currentTankDiameter);
            upperBottomCapModule.updateScaleForDiameter(currentTankDiameter);

            float mountDiameterScale = currentTankDiameter;
            if (splitTank)
            {
                currentIntertankModule.updateScaleForDiameter(currentTankDiameter);
                float lowerDiameter = currentTankDiameter * 0.75f;
                float lowerHeight = currentTankHeight * 0.75f;
                mountDiameterScale = lowerDiameter;
                lowerModule.updateScaleForHeightAndDiameter(lowerHeight, lowerDiameter);
                lowerBottomCapModule.updateScaleForDiameter(lowerDiameter);
            }            
            currentMountModule.updateScaleForDiameter(mountDiameterScale);
            rcsModule.updateScaleForDiameter(mountDiameterScale);
        }
                
        /// <summary>
        /// Updated the modules internal cached position value.  This value is used later to update the actual model positions.
        /// </summary>
        private void updateModulePositions()
        {
            float totalHeight = 0;
            totalHeight += upperTopCapModule.currentHeight;
            totalHeight += upperModule.currentHeight;
            totalHeight += upperBottomCapModule.currentHeight;

            if (splitTank)
            {
                totalHeight += currentIntertankModule.currentHeight;
                totalHeight += lowerModule.currentHeight;
                totalHeight += lowerBottomCapModule.currentHeight;
            }
            totalHeight += currentMountModule.currentHeight;
            
            float startY = totalHeight * 0.5f;
            partTopY = startY;
                        
            topFairingBottomY = partTopY - upperTopCapModule.currentHeight + (upperTopCapModule.modelDefinition.fairingTopOffset * upperTopCapModule.currentHeightScale);
            partBottomY = -startY;           

            startY -= upperTopCapModule.currentHeight;
            upperTopCapModule.currentVerticalPosition = startY;            
            
            startY -= upperModule.currentHeight * 0.5f;
            upperModule.currentVerticalPosition = startY;
            startY -= upperModule.currentHeight * 0.5f;         
            upperBottomCapModule.currentVerticalPosition = startY;

            startY -= upperBottomCapModule.currentHeight;
            if (splitTank)
            {
                currentIntertankModule.currentVerticalPosition = startY;
                startY -= currentIntertankModule.currentHeight;
                startY -= lowerModule.currentHeight * 0.5f;
                lowerModule.currentVerticalPosition = startY;
                startY -= lowerModule.currentHeight * 0.5f;
                lowerBottomCapModule.currentVerticalPosition = startY;
                startY -= lowerBottomCapModule.currentHeight;
            }

            currentMountModule.currentVerticalPosition = startY;
            rcsModule.currentVerticalPosition = currentMountModule.currentVerticalPosition + (currentMountModule.modelDefinition.rcsVerticalPosition * currentMountModule.currentHeightScale);
            rcsModule.currentHorizontalPosition = currentMountModule.modelDefinition.rcsHorizontalPosition * currentMountModule.currentDiameterScale;
            rcsModule.mountVerticalRotation = currentMountModule.modelDefinition.rcsVerticalRotation;
            rcsModule.mountHorizontalRotation = currentMountModule.modelDefinition.rcsHorizontalRotation;

            if (splitTank)
            {
                
                bottomFairingTopY = currentIntertankModule.currentVerticalPosition;
                float offset = currentIntertankModule.modelDefinition.fairingTopOffset * currentIntertankModule.currentHeightScale;                
                bottomFairingTopY -= offset;
            }
            else
            {
                bottomFairingTopY = currentMountModule.currentVerticalPosition;
                bottomFairingTopY += currentMountModule.modelDefinition.fairingTopOffset * currentMountModule.currentHeightScale;
            }
        }

        /// <summary>
        /// Blanket method for when module parameters have changed (heights, diameters, mounts, etc)
        /// updates
        /// Does not update fuel/resources/mass
        /// </summary>
        private void updateModules(bool userInput)
        {
            updateModuleScales();
            updateModulePositions();
            updateNodePositions(userInput);
            updateFairing(userInput || (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor));
        }
                
        /// <summary>
        /// Update the fairing module height and position based on current tank parameters
        /// </summary>
        private void updateFairing(bool userInput)
        {
            SSTUNodeFairing[] modules = part.GetComponents<SSTUNodeFairing>();
            if (modules == null || modules.Length < 2)
            {
                return;                
            }
            SSTUNodeFairing topFairing = modules[topFairingIndex];
            if (topFairing != null)
            {
                FairingUpdateData data = new FairingUpdateData();
                data.setTopY(partTopY);
                data.setBottomY(topFairingBottomY);
                data.setBottomRadius(currentTankDiameter * 0.5f);
                if (userInput){data.setTopRadius(currentTankDiameter * 0.5f);}
                topFairing.updateExternal(data);
            }            
            SSTUNodeFairing bottomFairing = modules[lowerFairingIndex];
            if (bottomFairing != null)
            {
                FairingUpdateData data = new FairingUpdateData();
                data.setTopRadius(currentTankDiameter * 0.5f);
                data.setTopY(bottomFairingTopY);
                if (userInput) { data.setBottomRadius(currentTankDiameter * 0.5f); }
                bottomFairing.updateExternal(data);
            }
        }

        /// <summary>
        /// Update the attach node positions based on the current tank parameters.
        /// </summary>
        private void updateNodePositions(bool userInput)
        {
            AttachNode topNode = part.findAttachNode("top");
            SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode, new Vector3(0, partTopY, 0), topNode.orientation, userInput);

            AttachNode topNode2 = part.findAttachNode("top2");
            SSTUAttachNodeUtils.updateAttachNodePosition(part, topNode2, new Vector3(0, topFairingBottomY, 0), topNode2.orientation, userInput);

            AttachNode bottomNode = part.findAttachNode("bottom");
            SSTUAttachNodeUtils.updateAttachNodePosition(part, bottomNode, new Vector3(0, partBottomY, 0), bottomNode.orientation, userInput);


            if (!String.IsNullOrEmpty(interstageNodeName))
            {
                Vector3 pos = new Vector3(0, bottomFairingTopY, 0);
                SSTUSelectableNodes.updateNodePosition(part, interstageNodeName, pos);
                AttachNode interstage = part.findAttachNode(interstageNodeName);
                if (interstage != null)
                {
                    Vector3 orientation = new Vector3(0, -1, 0);
                    SSTUAttachNodeUtils.updateAttachNodePosition(part, interstage, pos, orientation, userInput);
                }
            }
        }

        private void updateTextureSet(bool updateSymmetry)
        {
            currentMountModule.enableTextureSet(currentMountTexture);
            if (updateSymmetry)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    p.GetComponent<SSTUCustomUpperStage>().updateTextureSet(false);
                }
            }
        }

        #endregion

        #region ----------------- REGION - Model Build / Updating ----------------- 

        /// <summary>
        /// Builds the model from the current/default settings, and/or restores object links from existing game-objects
        /// </summary>
        private void buildSavedModel()
        {
            Transform modelBase = part.transform.FindRecursive("model").FindOrCreate(baseTransformName);

            setupModel(upperTopCapModule, modelBase, ModelOrientation.CENTRAL);
            setupModel(upperModule, modelBase, ModelOrientation.CENTRAL);
            setupModel(upperBottomCapModule, modelBase, ModelOrientation.CENTRAL);
            
            if (splitTank)
            {
                if (currentIntertankModule.name != defaultIntertank)
                {
                    SingleModelData dim = Array.Find<SingleModelData>(intertankModules, l => l.name == defaultIntertank);
                    dim.setupModel(part, modelBase, ModelOrientation.CENTRAL);
                    removeCurrentModel(dim);
                }
                setupModel(currentIntertankModule, modelBase, ModelOrientation.CENTRAL);
                setupModel(lowerModule, modelBase, ModelOrientation.CENTRAL);
                setupModel(lowerBottomCapModule, modelBase, ModelOrientation.CENTRAL);
            }
            if (currentMountModule.name != defaultMount)
            {
                MountModelData dmm = Array.Find<MountModelData>(mountModules, l => l.name == defaultMount);
                dmm.setupModel(part, modelBase, ModelOrientation.BOTTOM);
                removeCurrentModel(dmm);
            }

            setupModel(currentMountModule, modelBase, ModelOrientation.BOTTOM);
            setupModel(rcsModule, part.transform.FindRecursive("model").FindOrCreate(rcsTransformName), ModelOrientation.CENTRAL);
        }

        /// <summary>
        /// Finds the model for the given part, if it currently exists; else it clones it
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private void setupModel(ModelData model, Transform parent, ModelOrientation orientation)
        {
            model.setupModel(part, parent, orientation);
        }

        /// <summary>
        /// Removes the current model of the passed in upper-stage part; used when switching mounts or intertank parts
        /// </summary>
        /// <param name="usPart"></param>
        private void removeCurrentModel(ModelData usPart)
        {
            usPart.destroyCurrentModel();
        }

        /// <summary>
        /// Updates models from module current parameters for scale and positioning
        /// </summary>
        private void updateModels()
        {
            upperTopCapModule.updateModel();
            upperModule.updateModel();
            upperBottomCapModule.updateModel();

            if (splitTank)
            {
                currentIntertankModule.updateModel();
                lowerModule.updateModel();
                lowerBottomCapModule.updateModel();
            }

            currentMountModule.updateModel();
            rcsModule.updateModel();

            SSTUModInterop.onPartGeometryUpdate(part, true);
        }

        #endregion

        #region ----------------- REGION - Tank stats updating - volume/mass/cost/thrust ----------------- 

        /// <summary>
        /// Wrapper for methods that update the tanks internal statistics -- mass, volume, etc.  Does NOT update part resources.
        /// Calls: updateFuelVolume(); updatePartMass; updatePartCost; updateRCSThrust;
        /// </summary>
        private void updateTankStats()
        {
            updateFuelVolume();
            updateModuleMass();
            updateModuleCost();
            updateRCSThrust();
        }
        
        /// <summary>
        /// Calculates the internal volume from all of the currentl selected parts, their configurations, and their current scales
        /// </summary>
        private void updateFuelVolume()
        {
            totalTankVolume = 0;

            totalTankVolume += upperTopCapModule.getModuleVolume();
            totalTankVolume += upperModule.getModuleVolume();
            totalTankVolume += upperBottomCapModule.getModuleVolume();
            if(splitTank)
            {
                totalTankVolume += currentIntertankModule.getModuleVolume();
                totalTankVolume += lowerModule.getModuleVolume();
                totalTankVolume += lowerBottomCapModule.getModuleVolume();
            }
            totalTankVolume += currentMountModule.getModuleVolume();
        }

        /// <summary>
        /// Updates the cached part-mass value from the calculated masses of the current modules/tank setup.  Safe to call..whenever.
        /// Does -not- update part masses.  See -updatePartMass()- for that function.
        /// </summary>
        private void updateModuleMass()
        {
            moduleMass = upperTopCapModule.getModuleMass() + upperModule.getModuleMass() + upperBottomCapModule.getModuleMass() + currentMountModule.getModuleMass() + rcsModule.getModuleMass();
            if (splitTank)
            {
                moduleMass += currentIntertankModule.getModuleMass() + lowerModule.getModuleMass() + lowerBottomCapModule.getModuleMass();
            }
        }
                
        /// <summary>
        /// Updates the tankCost field with the current cost for the selected fuel type and tank size, including cost for tankage
        /// </summary>
        private void updateModuleCost()
        {
            moduleCost = upperTopCapModule.getModuleCost() + upperModule.getModuleCost() + upperBottomCapModule.getModuleCost() + currentMountModule.getModuleCost() + rcsModule.getModuleCost();
            if (splitTank)
            {
                moduleCost += currentIntertankModule.getModuleCost() + lowerModule.getModuleCost() + lowerBottomCapModule.getModuleCost();
            }
        }

        /// <summary>
        /// update external RCS-module with thrust value;
        /// TODO - may need to cache the 'needs update' flag, and run on first OnUpdate/etc, as otherwise the RCS module will likely not exist yet
        /// </summary>
        private void updateRCSThrust()
        {
            ModuleRCS[] rcsMod = part.GetComponents<ModuleRCS>();
            int len = rcsMod.Length;
            float scale = currentTankDiameter / defaultTankDiameter;
            rcsThrust = defaultRcsThrust * scale * scale;
            for (int i = 0; i < len; i++)
            {
                rcsMod[i].thrusterPower = rcsThrust;
            }
        }

        /// <summary>
        /// Updates current gui button availability status as well as updating the visible GUI variables from internal state vars
        /// </summary>
        private void updateGuiState()
        {
            Events["nextMountTextureEvent"].guiActiveEditor = currentMountModule.modelDefinition.textureSets.Length > 1;
            guiDryMass = moduleMass;
            guiTotalHeight = partTopY + Math.Abs(partBottomY);
            guiTankHeight = upperModule.currentHeight;
            guiRcsThrust = rcsThrust;
        }

        #endregion

        #region ----------------- REGION - Part Updating - Resource/Mass ----------------- 

        /// <summary>
        /// Updates the min/max quantities of resource in the part based on the current 'totalFuelVolume' field and currently set fuel type
        /// </summary>
        private void updateContainerVolume()
        {
            SSTUVolumeContainer container = part.GetComponent<SSTUVolumeContainer>();
            if (container != null) { container.onVolumeUpdated(totalTankVolume*1000f); }
        }        
        #endregion

    }

    public class SSTUCustomUpperStageRCS : ModelData
    {
        public GameObject[] models;
        public float currentHorizontalPosition;
        public float modelRotation = 0;
        public float modelHorizontalZOffset = 0;
        public float modelHorizontalXOffset = 0;
        public float modelVerticalOffset = 0;

        public float mountVerticalRotation = 0;
        public float mountHorizontalRotation = 0;

        public SSTUCustomUpperStageRCS(ConfigNode node) : base(node)
        {
            modelRotation = node.GetFloatValue("modelRotation");
            modelHorizontalZOffset = node.GetFloatValue("modelHorizontalZOffset");
            modelHorizontalXOffset = node.GetFloatValue("modelHorizontalXOffset");
            modelVerticalOffset = node.GetFloatValue("modelVerticalOffset");
        }
        
        public override void setupModel(Part part, Transform parent, ModelOrientation orientation)
        {
            models = new GameObject[4];
            Transform[] trs = part.transform.FindChildren(modelDefinition.modelName);
            if (trs != null && trs.Length>0)
            {
                for (int i = 0; i < 4; i++)
                {
                    models[i] = trs[i].gameObject;
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    models[i] = SSTUUtils.cloneModel(modelDefinition.modelName);
                }
            }
            foreach (GameObject go in models)
            {
                go.transform.NestToParent(parent);
            }            
        }

        public override void updateModel()
        {
            if (models != null)
            {
                float rotation = 0;
                float posX = 0, posZ = 0, posY = 0;
                float scale = 1;
                float length = 0;
                for (int i = 0; i < 4; i++)
                {
                    rotation = (float)(i * 90) + mountVerticalRotation;
                    scale = currentDiameterScale;
                    length = currentHorizontalPosition + (scale * modelHorizontalZOffset);                    
                    posX = (float)Math.Sin(SSTUUtils.toRadians(rotation)) * length;
                    posZ = (float)Math.Cos(SSTUUtils.toRadians(rotation)) * length;                    
                    posY = currentVerticalPosition + (scale * modelVerticalOffset);
                    models[i].transform.localScale = new Vector3(currentDiameterScale, currentHeightScale, currentDiameterScale);
                    models[i].transform.localPosition = new Vector3(posX, posY, posZ);
                    models[i].transform.localRotation = Quaternion.AngleAxis(rotation + 90f, new Vector3(0, 1, 0));
                    models[i].transform.Rotate(new Vector3(0, 0, 1), mountHorizontalRotation, Space.Self);
                }
            }
        }
    }
}
