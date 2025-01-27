﻿using AltV.Net;
using AltV.Net.Async;
using Roleplay.Entities;
using Roleplay.Factories;
using Roleplay.Models;
using System.Numerics;
using System.Text.Json;

namespace Roleplay.Scripts
{
    public class PropertyScript : IScript
    {
        [ClientEvent(nameof(BuyPropertyFurniture))]
        public static void BuyPropertyFurniture(MyPlayer player, int propertyId)
        {
            player.Emit("Server:CloseView");

            var furnitures = Global.Furnitures.Where(x => x.Category.ToLower() != "barreiras")
                .OrderBy(x => x.Category).ThenBy(x => x.Name).ToList();

            var html = $@"<div class='row'><div class='col-md-3'><select class='form-control' id='sel-category'><option value='Todas' selected>Todas</option>{string.Join("", furnitures.GroupBy(x => x.Category).Select(x => $"<option value='{x.Key}'>{x.Key}</option>").ToList())}</select></div><div class='col-md-9'><input id='pesquisa' type='text' autofocus class='form-control' placeholder='Pesquise as mobílias...' /></div></div><br/>
            <div class='table-responsive' style='max-height:50vh;overflow-y:auto;overflow-x:hidden;'>
                <table class='table table-bordered table-striped'>
                <thead>
                    <tr class='bg-dark'>
                        <th>Categoria</th>
                        <th>Nome</th>
                        <th>Objeto</th>
                        <th>Valor</th>
                        <th class='text-center'>Opções</th>
                    </tr>
                </thead>
                <tbody>";

            if (!furnitures.Any())
            {
                html += "<tr><td class='text-center' colspan='5'>Não há mobílias criadas.</td></tr>";
            }
            else
            {
                foreach (var furniture in furnitures)
                    html += $@"<tr class='pesquisaitem' data-category='{furniture.Category}'>
                        <td>{furniture.Category}</td>
                        <td>{furniture.Name}</td>
                        <td>{furniture.Model}</td>
                        <td>${furniture.Value:N0}</td>
                        <td class='text-center'>
                            <button onclick='buy(this, {furniture.Id})' type='button' class='btn btn-dark btn-sm'>COMPRAR</button>
                        </td>
                    </tr>";
            }

            html += $@"
                </tbody>
            </table>
            </div>";

            player.Emit("BuyPropertyFurnitures", propertyId, html);
        }

        [ClientEvent(nameof(SelectBuyPropertyFurniture))]
        public static void SelectBuyPropertyFurniture(MyPlayer player, int propertyId, int furnitureId)
        {
            var property = Global.Properties.FirstOrDefault(x => x.Id == propertyId);
            if (property == null)
                return;

            var furniture = Global.Furnitures.FirstOrDefault(x => x.Id == furnitureId);
            if (furniture == null)
                return;

            player.DropPropertyFurniture = new PropertyFurniture
            {
                PropertyId = propertyId,
                Model = furniture.Model,
                Interior = player.Dimension != 0,
            };
            player.Emit("DropObject", player.DropPropertyFurniture.Model, 2);
        }

        [ClientEvent(nameof(EditPropertyFurniture))]
        public static void EditPropertyFurniture(MyPlayer player, int propertyId, int propertyFurnitureId)
        {
            var property = Global.Properties.FirstOrDefault(x => x.Id == propertyId);
            if (property == null)
                return;

            player.DropPropertyFurniture = property.Furnitures.FirstOrDefault(x => x.Id == propertyFurnitureId);
            if (player.DropPropertyFurniture == null)
                return;

            player.DropPropertyFurniture.DeleteObject();
            player.Emit("DropObject", player.DropPropertyFurniture.Model, 2);
        }

        [AsyncClientEvent(nameof(RemovePropertyFurniture))]
        public static async Task RemovePropertyFurniture(MyPlayer player, int propertyId, int propertyFurnitureId)
        {
            var property = Global.Properties.FirstOrDefault(x => x.Id == propertyId);
            if (property == null)
                return;

            var propertyFurniture = property.Furnitures.FirstOrDefault(x => x.Id == propertyFurnitureId);
            if (propertyFurniture == null)
                return;

            await using var context = new DatabaseContext();
            context.PropertiesFurnitures.Remove(propertyFurniture);
            await context.SaveChangesAsync();

            property.Furnitures.Remove(propertyFurniture);
            propertyFurniture.DeleteObject();

            player.SendMessage(MessageType.Success, $"Você removeu a mobília {propertyFurniture.Id}.", notify: true);
            player.Emit("PropertyFurnitures", property.Id, property.GetFurnituresHTML(player));
        }

        [ClientEvent(nameof(CancelDropFurniture))]
        public static void CancelDropFurniture(MyPlayer player)
        {
            if (player.DropPropertyFurniture == null)
                return;

            var property = Global.Properties.FirstOrDefault(x => x.Id == player.DropPropertyFurniture.PropertyId);
            if (property == null)
                return;

            player.SendMessage(MessageType.Success, "Você cancelou o drop da mobília.", notify: true);

            if (player.DropPropertyFurniture.Id > 0)
            {
                player.DropPropertyFurniture.CreateObject();
                player.Emit("PropertyFurnitures", property.Id, property.GetFurnituresHTML(player));
            }
            else
            {
                BuyPropertyFurniture(player, player.DropPropertyFurniture.PropertyId);
            }

            player.DropPropertyFurniture = null;
        }

        [AsyncClientEvent(nameof(ConfirmDropFurniture))]
        public static async Task ConfirmDropFurniture(MyPlayer player, Vector3 position, Vector3 rotation)
        {
            if (player.DropPropertyFurniture == null)
                return;

            if (position.X == 0 || position.Y == 0 || position.Z == 0)
            {
                player.SendMessage(MessageType.Error, "Não foi possível recuperar a posição do item.");
                return;
            }

            var property = Global.Properties.FirstOrDefault(x => x.Id == player.DropPropertyFurniture.PropertyId);
            if (property == null)
                return;

            var furniture = Global.Furnitures.FirstOrDefault(x => x.Model == player.DropPropertyFurniture.Model && x.Value > 0);
            if (furniture == null)
                return;

            var newFurniture = false;
            if (player.DropPropertyFurniture.Id == 0)
            {
                newFurniture = true;

                var maxFurnitures = 100;
                if ((player.User.VIPValidDate ?? DateTime.MinValue) >= DateTime.Now)
                {
                    if (player.User.VIP == UserVIP.Gold)
                        maxFurnitures = 250;
                    else if (player.User.VIP == UserVIP.Silver)
                        maxFurnitures = 200;
                    else if (player.User.VIP == UserVIP.Bronze)
                        maxFurnitures = 150;
                }

                if (property.Furnitures.Count == maxFurnitures)
                {
                    player.SendMessage(MessageType.Error, $"O limite de {maxFurnitures} mobílias da propriedade foi atingido.", notify: true);
                    return;
                }

                if (player.Money < furniture.Value)
                {
                    player.SendMessage(MessageType.Error, string.Format(Global.INSUFFICIENT_MONEY_ERROR_MESSAGE, furniture.Value), notify: true);
                    return;
                }

                await player.RemoveItem(new CharacterItem(ItemCategory.Money) { Quantity = furniture.Value });
            }

            player.DropPropertyFurniture.PosX = position.X;
            player.DropPropertyFurniture.PosY = position.Y;
            player.DropPropertyFurniture.PosZ = position.Z;
            player.DropPropertyFurniture.RotR = rotation.X;
            player.DropPropertyFurniture.RotP = rotation.Y;
            player.DropPropertyFurniture.RotY = rotation.Z;

            await using var context = new DatabaseContext();

            if (player.DropPropertyFurniture.Id == 0)
                await context.PropertiesFurnitures.AddAsync(player.DropPropertyFurniture);
            else
                context.PropertiesFurnitures.Update(player.DropPropertyFurniture);

            await context.SaveChangesAsync();

            if (!property.Furnitures.Contains(player.DropPropertyFurniture))
                property.Furnitures.Add(player.DropPropertyFurniture);

            player.DropPropertyFurniture.CreateObject();

            await player.GravarLog(LogType.EditPropertyFurniture, JsonSerializer.Serialize(player.DropPropertyFurniture), null);

            if (newFurniture)
            {
                player.SendMessage(MessageType.Success, $"Você comprou {furniture.Name} por ${furniture.Value:N0}.", notify: true);
                BuyPropertyFurniture(player, player.DropPropertyFurniture.PropertyId);
            }
            else
            {
                player.SendMessage(MessageType.Success, $"Você editou a posição de {furniture.Name}.", notify: true);
                player.Emit("PropertyFurnitures", property.Id, property.GetFurnituresHTML(player));
            }

            player.DropPropertyFurniture = null;
        }
    }
}