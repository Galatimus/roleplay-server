﻿using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using Roleplay.Factories;
using Roleplay.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Roleplay.Entities
{
    public class Property
    {
        public int Id { get; set; }

        public PropertyInterior Interior { get; set; }

        public int Value { get; set; }

        [ForeignKey(nameof(Character))]
        public int? CharacterId { get; set; }

        [JsonIgnore]
        public Character Character { get; set; }

        public float EntrancePosX { get; set; }

        public float EntrancePosY { get; set; }

        public float EntrancePosZ { get; set; }

        public float ExitPosX { get; set; }

        public float ExitPosY { get; set; }

        public float ExitPosZ { get; set; }

        public int Dimension { get; set; }

        public string Address { get; set; }

        public uint LockNumber { get; set; }

        public bool Locked { get; set; }

        public byte ProtectionLevel { get; set; }

        public int RobberyValue { get; set; }

        public DateTime? RobberyCooldown { get; set; }

        [JsonIgnore]
        public ICollection<PropertyFurniture> Furnitures { get; set; } = new List<PropertyFurniture>();

        [NotMapped, JsonIgnore]
        public List<PropertyItem> Items { get; set; } = new();

        [NotMapped, JsonIgnore]
        public IMarker EntranceMarker { get; set; }

        [NotMapped, JsonIgnore]
        public MyColShape EntranceColShape { get; set; }

        [NotMapped, JsonIgnore]
        public AudioSpot ExteriorAlarmAudioSpot { get; set; }

        [NotMapped, JsonIgnore]
        public AudioSpot InteriorAlarmAudioSpot { get; set; }

        [Obsolete("TODO: Rollback commentary when alt:V implements")]
        public void CreateIdentifier()
        {
            RemoveIdentifier();

            var pos = new Vector3(EntrancePosX, EntrancePosY, EntrancePosZ - 0.95f);

            //EntranceMarker = Alt.CreateMarker(MarkerType.MarkerHalo, pos, Global.MainRgba);
            //EntranceMarker.Scale = new Vector3(1, 1, 1.5f);
            //EntranceMarker.Dimension = Dimension;

            EntranceColShape = (MyColShape)Alt.CreateColShapeCylinder(pos, 1, 1.5f);
            EntranceColShape.Description = $"[PROPRIEDADE Nº {Id}] {{#FFFFFF}}{(!CharacterId.HasValue ? $"Use /comprar para comprar por ${Value:N0}." : string.Empty)}";
        }

        public void RemoveIdentifier()
        {
            EntranceMarker?.Destroy();
            EntranceMarker = null;

            EntranceColShape?.Destroy();
            EntranceColShape = null;
        }

        public void ShowInventory(MyPlayer player, bool update)
        {
            player.ShowInventory(player, InventoryShowType.Property,
                $"Propriedade Nº {Id}",
                JsonSerializer.Serialize(
                    Items.Select(x => new
                    {
                        ID = x.Id,
                        x.Image,
                        x.Name,
                        x.Quantity,
                        Slot = 1000 + x.Slot,
                        Extra = Functions.GetItemExtra(x),
                        Weight = (x.Quantity * x.Weight).ToString("N2"),
                    })
            ), update, Id);
        }

        public bool CanAccess(MyPlayer player)
        {
            return CharacterId == player.Character.Id ||
                player.Items.Any(x => x.Category == ItemCategory.PropertyKey && x.Type == LockNumber);
        }

        public async Task ActivateProtection()
        {
            if (ProtectionLevel >= 1)
                StartAlarm();

            if (ProtectionLevel >= 2)
            {
                var target = Global.Players.FirstOrDefault(x => x.Character.Id == CharacterId && x.Cellphone > 0 && !x.CellphoneItem.ModoAviao);
                target?.SendMessage(MessageType.None, $"[CELULAR] SMS de {target.ObterNomeContato(Global.EMERGENCY_NUMBER)}: O alarme da sua propriedade {Id} em {Address} foi acionado.", Global.CELLPHONE_MAIN_COLOR);
            }

            if (ProtectionLevel >= 3)
            {
                var emergencyCall = new EmergencyCall
                {
                    Type = EmergencyCallType.Police,
                    Number = Global.EMERGENCY_NUMBER,
                    PosX = EntrancePosX,
                    PosY = EntrancePosY,
                    Message = $"O alarme da propriedade {Id} foi acionado.",
                    Location = Address,
                };
                await using var context = new DatabaseContext();
                await context.EmergencyCalls.AddAsync(emergencyCall);
                await context.SaveChangesAsync();
                Global.EmergencyCalls.Add(emergencyCall);
                emergencyCall.SendMessage();
            }
        }

        public void StartAlarm()
        {
            if (ExteriorAlarmAudioSpot == null)
            {
                ExteriorAlarmAudioSpot = new AudioSpot
                {
                    Position = new Vector3(EntrancePosX, EntrancePosY, EntrancePosZ),
                    Dimension = Dimension,
                    Source = Global.URL_AUDIO_PROPERTY_ALARM,
                    Loop = true,
                };
                ExteriorAlarmAudioSpot.SetupAllClients();
            }

            if (InteriorAlarmAudioSpot == null)
            {
                InteriorAlarmAudioSpot = new AudioSpot
                {
                    Position = new Vector3(ExitPosX, ExitPosY, ExitPosZ),
                    Dimension = Dimension,
                    Source = Global.URL_AUDIO_PROPERTY_ALARM,
                    Loop = true,
                };
                InteriorAlarmAudioSpot.SetupAllClients();
            }
        }

        public void StopAlarm()
        {
            ExteriorAlarmAudioSpot?.RemoveAllClients();
            ExteriorAlarmAudioSpot = null;

            InteriorAlarmAudioSpot?.RemoveAllClients();
            InteriorAlarmAudioSpot = null;
        }

        public string GetFurnituresHTML(MyPlayer player)
        {
            var html = string.Empty;
            if (!Furnitures.Any())
            {
                html = "<tr><td class='text-center' colspan='7'>Não há mobílias na propriedade.</td></tr>";
            }
            else
            {
                foreach (var furniture in Furnitures.OrderBy(x => player.Position.Distance(new Position(x.PosX, x.PosY, x.PosZ))))
                    html += $@"<tr class='pesquisaitem'>
                        <td>{furniture.Id}</td>
                        <td>{furniture.ModelName}</td>
                        <td>X: {furniture.PosX} | Y: {furniture.PosY} | Z: {furniture.PosZ}</td>
                        <td>R: {furniture.RotR} | P: {furniture.RotP} | Y: {furniture.RotY}</td>
                        <td>{player.Position.Distance(new Position(furniture.PosX, furniture.PosY, furniture.PosZ))}</td>
                        <td class='text-center'>{(furniture.Interior ? "SIM" : "NÃO")}</td>
                        <td class='text-center'>
                            <button onclick='edit(this, {furniture.Id})' type='button' class='btn btn-dark btn-sm'>EDITAR</button>
                            <button onclick='remove(this, {furniture.Id})' type='button' class='btn btn-danger btn-sm'>EXCLUIR</button>
                        </td>
                    </tr>";
            }
            return html;
        }
    }
}