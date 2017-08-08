﻿using SabberStoneCore.Model;
using SabberStoneKettle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using SabberStoneCore.Enums;

namespace SabberStoneKettleClient
{
	class KettleAISession
    {
        private KettleStartClient StartClient;
        private KettleAdapter Adapter;
        private int PlayerId;
        private Game Game;

        private static readonly Random Rand = new Random();

        public KettleAISession(KettleStartClient startClient)
        {
            this.StartClient = startClient;
        }

        public void Enter()
        {
            // first we connect to the gameserver
            var socket = new Socket(SocketType.Stream, ProtocolType.IP);
            socket.Connect(StartClient.IP, StartClient.Port);
            Adapter = new KettleAdapter(new NetworkStream(socket));
            Adapter.OnGameJoined += OnGameJoined;
            Adapter.OnEntityChoices += OnEntityChoices;
            Adapter.OnEntitiesChosen += OnEntitiesChosen;
            Adapter.OnOptionsBlock += OnOptionsBlock;
            Adapter.OnUserUI += OnUserUI;
            Adapter.OnHistory += OnHistory;

            Adapter.SendMessage(StartClient.JoinGame);

            // and then we keep listening on the socket
            while (true)
            {
                if (!Adapter.HandleNextPacket())
                {
                    Console.WriteLine("Kettle session ended.");
                    return;
                }
            }
        }

        public void OnGameJoined(KettleGameJoined gameJoined)
        {
            Console.WriteLine("AI OnGameJoined called.");
        }

        public void OnEntityChoices(KettleEntityChoices entityChoices)
        {
            Console.WriteLine("AI EntityChoices called.");
            if (entityChoices.PlayerId != PlayerId)
                return;

            var pickList = new List<int>();
            switch ((ChoiceType) entityChoices.ChoiceType)
            {
                case ChoiceType.MULLIGAN:
                    var picks = Rand.Next(0, entityChoices.Entities.Count);
                    pickList.AddRange(entityChoices.Entities);
                    for (var i = 0; i < picks; i++)
                    {
                        pickList.Remove(pickList[Rand.Next(0, pickList.Count)]);
                    }
                    break;
                case ChoiceType.GENERAL:
                    pickList.Add(entityChoices.Entities[Rand.Next(0, entityChoices.Entities.Count)]);
                    break;
                case ChoiceType.INVALID:
                default:
                    throw new NotImplementedException();
            }

            Adapter.SendMessage(new KettleChooseEntities()
            {
                Id = entityChoices.Id,
                Choices = pickList
            });
        }

        public void OnEntitiesChosen(KettleEntitiesChosen entitiesChosen)
        {
            Console.WriteLine("AI OnEntitiesChosen called.");
        }

        public void OnOptionsBlock(KettleOptionsBlock optionsBlock)
        {
            Console.WriteLine("AI OnOptionsBlock called.");

            int kettleOptionIndex = Rand.Next(0, optionsBlock.Options.Count);
            var kettleOption = optionsBlock.Options[kettleOptionIndex];

            var kettleSendOption = new KettleSendOption()
            {
                Id = optionsBlock.Id,
            };

            if (kettleOption.MainOption != null)
            {
                kettleSendOption.MainOption = kettleOptionIndex;
            }

            if (kettleOption.SubOptions != null && kettleOption.SubOptions.Any())
            {
                var kettleSubOptionIndex = Rand.Next(0, kettleOption.SubOptions.Count);
                var subOption = kettleOption.SubOptions[kettleSubOptionIndex];
                kettleSendOption.SubOption = kettleSubOptionIndex;

                if (subOption.Targets != null && subOption.Targets.Any())
                {
                    kettleSendOption.Target = subOption.Targets[Rand.Next(0, subOption.Targets.Count)];
                }
            }
            else
            {
                if (kettleOption.MainOption != null && kettleOption.MainOption.Targets != null && kettleOption.MainOption.Targets.Any())
                {
                    kettleSendOption.Target = kettleOption.MainOption.Targets[Rand.Next(0, kettleOption.MainOption.Targets.Count)];
                }
            }

            kettleSendOption.Position = 0;

            Adapter.SendMessage(kettleSendOption);
        }

        public void OnUserUI(KettleUserUI userUi)
        {
            Console.WriteLine("AI OnUserUI called.");
        }

        public void OnHistory(List<KettleHistoryEntry> history)
        {
            var createGame = history.Where(p => p is KettleHistoryCreateGame).Select(p => p as KettleHistoryCreateGame).FirstOrDefault();
            if (createGame != null)
            {
                var player = createGame.Players.Where(p => p.AccountId == StartClient.JoinGame.AccountId).First();
                PlayerId = player.Entity.EntityId - 1;

                // TODO create the Game object here, etc
            }

            Console.WriteLine("AI OnHistory called.");
        }
    }
}
