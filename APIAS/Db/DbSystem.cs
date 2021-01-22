﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using APIAS.Abstracts;
using APIAS.Data;
using Discord;
using Newtonsoft.Json.Linq;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using APIAS.Extensions;

namespace APIAS.Db
{
    class DbSystem
    {
        public RethinkDB R1 { get; }

        private Connection _conn;
        private const string _dbName = "ApiasDb";

        private const string _guildTableName = "Guilds";

        public DbSystem()
        {
            R1 = RethinkDB.R;
        }

        public async Task InitAsync()
            => await InitAsync(_dbName);

        public async Task InitAsync(string DbName)
        {
            _conn = await R1.Connection().ConnectAsync();
            if (!await R1.DbList().Contains(DbName).RunAsync<bool>(_conn)) {
                R1.DbCreate(DbName).Run(_conn);
            }
            if (!await R1.Db(DbName).TableList().Contains(_guildTableName).RunAsync<bool>(_conn)) {
                R1.Db(DbName).TableCreate(_guildTableName).Run(_conn);
            }
        }

        public async Task InitGuildAsync(IGuild guild)
        {
            string GuildID = guild.Id.ToString();
            if (await R1.Db(_dbName).Table(_guildTableName).GetAll(GuildID).Count().Eq(0).RunAsync<bool>(_conn)) {
                Server server = new Server
                {
                    id = GuildID,
                    ServerName = guild.Name,
                    Follows = new List<AFollow>()
                };

                await R1.Db(_dbName).Table(_guildTableName).Insert(server).RunAsync(_conn);
            }
        }

        public async Task FetchGuildsAsync()
        {
            var AllGuilds = await R1.Db(_dbName).Table(_guildTableName).RunAsync<JObject>(_conn);
            foreach (JObject guild in AllGuilds.BufferedItems)
            {
                Server Temp = new Server(guild);
                AddToActives(Temp.Follows);
            }
        }

        public async Task RemoveFollowAsync(AFollow follow, string GuildID)
        {
            JObject GuildObject = await R1.Db(_dbName).Table(_guildTableName).Get(GuildID).RunAsync<JObject>(_conn);
            Server Guild = new Server(GuildObject);
            Guild.Follows.Remove(follow);
            await R1.Db(_dbName).Table(_guildTableName).Update(Guild).RunAsync(_conn);
        }

        public async Task GetGuildAsync(string GuildID)
        {
            JObject GuildObject = await R1.Db(_dbName).Table(_guildTableName).Get(GuildID).RunAsync<JObject>(_conn);
            Server Guild = new Server(GuildObject);
            AddToActives(Guild.Follows);
        }

        public async Task AddFollowToGuildAsync(AFollow Follow, string GuildID)
        {
            JObject GuildObject = await R1.Db(_dbName).Table(_guildTableName).Get(GuildID).RunAsync<JObject>(_conn);
            Server Guild = new Server(GuildObject);
            Guild.Follows.AddUnique(Follow);
            await R1.Db(_dbName).Table(_guildTableName).Update(Guild).RunAsync(_conn);
        }

        public async Task UpdateFollowAsync(AFollow Follow)
        {
            JObject GuildObject = await R1.Db(_dbName).Table(_guildTableName).Get(Follow.GuildID).RunAsync<JObject>(_conn);
            Server Guild = new Server(GuildObject);
            Guild.Follows.Remove(Guild.Follows.Where(x => x.FollowDbName == Follow.FollowDbName).FirstOrDefault());
            Guild.Follows.AddUnique(Follow);
            await R1.Db(_dbName).Table(_guildTableName).Update(Guild).RunAsync(_conn);
        }

        private void AddToActives(List<AFollow> list)
        {
            foreach (AFollow follow in list)
            {
                follow.InitTimer();
                Globals.ActiveFollows.AddUnique(follow);
            }
        }
    }
}
