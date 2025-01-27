﻿using AltV.Net;
using AltV.Net.Elements.Entities;

namespace Roleplay.Factories
{
    public class MyPlayerFactory : IEntityFactory<IPlayer>
    {
        public IPlayer Create(ICore server, nint playerPointer, uint id)
        {
            return new MyPlayer(server, playerPointer, id);
        }
    }
}