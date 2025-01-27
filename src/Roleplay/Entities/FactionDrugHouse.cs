﻿using AltV.Net;
using AltV.Net.Elements.Entities;
using Roleplay.Factories;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Roleplay.Entities
{
    public class FactionDrugHouse
    {
        public int Id { get; set; }

        public int FactionId { get; set; }

        public float PosX { get; set; }

        public float PosY { get; set; }

        public float PosZ { get; set; }

        public int Dimension { get; set; }

        [NotMapped, JsonIgnore]
        public IMarker Marker { get; set; }

        [NotMapped, JsonIgnore]
        public MyColShape ColShape { get; set; }

        [Obsolete("TODO: Rollback commentary when alt:V implements")]
        public void CreateIdentifier()
        {
            RemoveIdentifier();

            var pos = new Vector3(PosX, PosY, PosZ - 0.95f);

            //Marker = Alt.CreateMarker(MarkerType.MarkerHalo, pos, Global.MainRgba);
            //Marker.Scale = new Vector3(1, 1, 1.5f);
            //Marker.Dimension = Dimension;

            ColShape = (MyColShape)Alt.CreateColShapeCylinder(pos, 1, 1.5f);
            ColShape.Dimension = Dimension;
            ColShape.Description = $"[DRUG HOUSE] {{#FFFFFF}}Use /drughouse.";
        }

        public void RemoveIdentifier()
        {
            Marker?.Destroy();
            Marker = null;

            ColShape?.Destroy();
            ColShape = null;
        }
    }
}