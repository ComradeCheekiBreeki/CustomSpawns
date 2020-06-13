﻿using CustomSpawns.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.Localization;

namespace CustomSpawns.Spawn
{
    class DailySpawnBehaviour : CampaignBehaviorBase
    {

        #region Data Management

        Data.SpawnDataManager dataManager;

        private int lastRedundantDataUpdate = 0;

        public DailySpawnBehaviour(Data.SpawnDataManager data_manager)
        {
            DynamicSpawnData.FlushSpawnData();
            lastRedundantDataUpdate = 0;
            GetCurrentData();
            dataManager = data_manager;
        }

        public void GetCurrentData()
        {
            foreach (MobileParty mb in MobileParty.All)
            {
                foreach (var dat in dataManager.Data)
                {
                    if (CampaignUtils.IsolateMobilePartyStringID(mb) == dat.PartyTemplate.StringId)
                    {
                        //this be a custom spawns party :O
                        DynamicSpawnData.AddDynamicSpawnData(mb, new CSPartyData(dat, null));
                        UpdateDynamicData(mb);
                        UpdateRedundantDynamicData(mb);
                    }
                }
            }

        }

        public void HourlyCheckData()
        {
            if (lastRedundantDataUpdate < ConfigLoader.Instance.Config.UpdatePartyRedundantDataPerHour + 1) // + 1 to give leeway and make sure every party gets updated. 
            {
                lastRedundantDataUpdate++;
            }
            else
            {
                lastRedundantDataUpdate = 0;
            }

            //Now for data checking!
        }

        public void UpdateDynamicData(MobileParty mb)
        {

        }

        public void UpdateRedundantDynamicData(MobileParty mb)
        {
            DynamicSpawnData.GetDynamicSpawnData(mb).latestClosestSettlement = CampaignUtils.GetClosestSettlement(mb);
        }

        #endregion


        #region MB API-Registered Behaviours

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyBehaviour);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, HourlyBehaviour);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, HourlyPartyBehaviour);
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
        }

        public override void SyncData(IDataStore dataStore)
        {

        }

        private bool spawnedToday = false;

        private void HourlyBehaviour()
        {
            HourlyCheckData();
            if (!spawnedToday && Campaign.Current.IsNight)
            {
                RegularBanditSpawn();
                spawnedToday = true;
            }

        }

        //deal with our parties being removed! Also this is more efficient ;)
        private void OnPartyRemoved(PartyBase p)
        {
            MobileParty mb = p.MobileParty;
            if (mb == null)
                return;

            CSPartyData partyData = DynamicSpawnData.GetDynamicSpawnData(mb);
            if (partyData != null)
            {
                partyData.spawnBaseData.DecrementNumberSpawned();
                //this is a custom spawns party!!
                OnPartyDeath(mb, partyData);
                DynamicSpawnData.RemoveDynamicSpawnData(mb);
            }
        }

        private void HourlyPartyBehaviour(MobileParty mb)
        {
            UpdateDynamicData(mb);
            if (lastRedundantDataUpdate >= ConfigLoader.Instance.Config.UpdatePartyRedundantDataPerHour)
            {
                UpdateRedundantDynamicData(mb);
            }
        }

        private void DailyBehaviour()
        {
            spawnedToday = false;
        }

        #endregion

        private void RegularBanditSpawn()
        {
            try
            {
                var list = dataManager.Data;
                Random rand = new Random();
                foreach (Data.SpawnData data in list)
                {
                    int j = 0;
                    for (int i = 0; i < data.RepeatSpawnRolls; i++)
                    {
                        if (data.CanSpawn() && (data.MinimumNumberOfDaysUntilSpawn < (int)Math.Ceiling(Campaign.Current.CampaignStartTime.ElapsedDaysUntilNow)))
                        {
                            if (ConfigLoader.Instance.Config.IsAllSpawnMode || (float)rand.NextDouble() < data.ChanceOfSpawn)
                            {
                                var spawnSettlement = Spawner.GetSpawnSettlement(data, rand);
                                //spawn nao!
                                MobileParty spawnedParty = Spawner.SpawnParty(spawnSettlement, data.SpawnClan, data.PartyTemplate, data.PartyType, new TextObject(data.Name));
                                data.IncrementNumberSpawned(); //increment for can spawn and chance modifications
                                j++;
                                //AI Checks!
                                Spawner.HandleAIChecks(spawnedParty, data, spawnSettlement);
                                //accompanying spawns
                                foreach (var accomp in data.SpawnAlongWith)
                                {
                                    MobileParty juniorParty = Spawner.SpawnParty(spawnSettlement, data.SpawnClan, accomp.templateObject, data.PartyType, new TextObject(accomp.name));
                                    Spawner.HandleAIChecks(juniorParty, data, spawnSettlement); //junior party has same AI behaviour as main party. TODO in future add some junior party AI and reconstruction.
                                }
                                //message if available
                                if (data.spawnMessage != null)
                                {
                                    UX.ShowParseSpawnMessage(data.spawnMessage, spawnSettlement.Name.ToString());
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    data.SetNumberSpawned(data.GetNumberSpawned() - j); //make sure that only the hourly checker really tells number spawned.
                }
            }
            catch (Exception e)
            {
                ErrorHandler.HandleException(e);
            }
        }
        private void OnPartyDeath(MobileParty mb, CSPartyData dynamicData)
        {

        }
    }
}
