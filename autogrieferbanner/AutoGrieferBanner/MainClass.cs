using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Linq;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using MySql.Data.MySqlClient;

namespace AutoGrieferBanner
{
    [APIVersion(1, 11)]
    public class AutoBanner : TerrariaPlugin
    {
       // public static ABconfigClass ABconfig { get; set; }
        public static SqlTableEditor SQLEditor;
        public static SqlTableCreator SQLWriter;
        public static List<PlayerObj> playerList = new List<PlayerObj>();
        public static List<RegionObj> regionList = new List<RegionObj>();

        public override string Name
        {
            get { return "Auto Griefer Banner"; }
        }
        public override string Author
        {
            get { return "by InanZen"; }
        }
        public override string Description
        {
            get { return "Auto bans the Grievers in defined regions"; }
        }
        public override Version Version
        {
            get { return new Version("1.0"); }
        }
        public override void Initialize()
        {  
            GameHooks.Initialize += OnInitialize;
            ServerHooks.Join += OnJoin;
            ServerHooks.Leave += OnLeave;
            GetDataHandlers.TileEdit += TileEdit;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= OnInitialize;
                ServerHooks.Join -= OnJoin;
                ServerHooks.Leave -= OnLeave;
                GetDataHandlers.TileEdit -= TileEdit;
            }
            base.Dispose(disposing);
        }
        public AutoBanner(Main game)
            : base(game)
        {
            Order = -1;
        }
        public void OnInitialize()
        {
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            var table = new SqlTable("AutoBanRegions",
                 new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                 new SqlColumn("Name", MySqlDbType.VarChar, 255) { Unique = true },
                 new SqlColumn("X", MySqlDbType.Int32),
                 new SqlColumn("Y", MySqlDbType.Int32),
                 new SqlColumn("Width", MySqlDbType.Int32),
                 new SqlColumn("Height", MySqlDbType.Int32),
                 new SqlColumn("Owners", MySqlDbType.Text)
             );
            SQLWriter.EnsureExists(table);
            RegionTools.ReadRegions();
            Commands.ChatCommands.Add(new Command("abcommands", ABCommands.ABCommand, "ab"));
        }
        public void OnJoin(int who, HandledEventArgs e)
        {
            lock (playerList)
                playerList.Add(new PlayerObj(who));
        }
        public void OnLeave(int who)
        {
            lock (playerList)
            {
                for (int i = 0; i < playerList.Count; i++)
                {
                    if (playerList[i].Index == who)
                    {
                        RevertTiles(playerList[i]);
                        playerList.RemoveAt(i);
                        break;
                    }
                }
            }
        }   
        void TileEdit(Object sender, TShockAPI.GetDataHandlers.TileEditEventArgs args)
        {
            PlayerObj ply = GetPlayerByID(args.Player.UserID);
            string regName = RegionTools.InAreaRegionName(args.X, args.Y);
            if (ply.AwaitingRegionName)
            {
                if (regName == null) { args.Player.SendMessage("Region is not protected by AutoBan"); }
                else { args.Player.SendMessage("AutoBan region: " + regName); }                
                ply.AwaitingRegionName = false;                
                args.Player.SendTileSquare(args.X, args.Y, 1);
                args.Handled = true;
            }
            else if (args.Player.UserID > 0 && regName != null)
            {
                if (RegionTools.IsOwner(args.Player.UserID.ToString(), regName) || args.Player.Group.HasPermission("abedit") || args.Player.Group.Name == "superadmin")
                { 
                    //do nothing :)
                }
                else
                {
                    TileObj newTile = new TileObj();
                    newTile.X = args.X;
                    newTile.Y = args.Y;
                    newTile.active = Main.tile[args.X, args.Y].active;
                    newTile.checkingLiquid = Main.tile[args.X, args.Y].checkingLiquid;
                    newTile.Data = Main.tile[args.X, args.Y].Data;
                    newTile.frameNumber = Main.tile[args.X, args.Y].frameNumber;
                    newTile.frameX = Main.tile[args.X, args.Y].frameX;
                    newTile.frameY = Main.tile[args.X, args.Y].frameY;
                    newTile.lava = Main.tile[args.X, args.Y].lava;
                    newTile.lighted = Main.tile[args.X, args.Y].lighted;
                    newTile.liquid = Main.tile[args.X, args.Y].liquid;
                    newTile.skipLiquid = Main.tile[args.X, args.Y].skipLiquid;
                    newTile.type = Main.tile[args.X, args.Y].type;
                    newTile.wall = Main.tile[args.X, args.Y].wall;
                    newTile.wallFrameNumber = Main.tile[args.X, args.Y].wallFrameNumber;
                    newTile.wallFrameX = Main.tile[args.X, args.Y].wallFrameX;
                    newTile.wallFrameY = Main.tile[args.X, args.Y].wallFrameY;
                    newTile.wire = Main.tile[args.X, args.Y].wire;
                    ply.tileList.Add(newTile);
                    if (ply.tileList.Count == 10)
                    {
                        args.Player.SendMessage("WARNING: If you continue to grief, you WILL get banned!", Color.Purple);
                    }
                    else if (ply.tileList.Count >= 20)
                    {
                        //RevertTiles(ply);
                        TShock.Utils.Ban(args.Player, "Griefing region: "+regName);                        
                    }                  
                }
            }
          
        }
        public void RevertTiles(PlayerObj player)
        {
            foreach (TileObj tile in player.tileList)
            {
                Main.tile[tile.X, tile.Y].active = tile.active;
                Main.tile[tile.X, tile.Y].checkingLiquid = tile.checkingLiquid;
                Main.tile[tile.X, tile.Y].Data = tile.Data;
                Main.tile[tile.X, tile.Y].frameNumber = tile.frameNumber;
                Main.tile[tile.X, tile.Y].frameX = tile.frameX;
                Main.tile[tile.X, tile.Y].frameY = tile.frameY;
                Main.tile[tile.X, tile.Y].lava = tile.lava;
                Main.tile[tile.X, tile.Y].lighted = tile.lighted;
                Main.tile[tile.X, tile.Y].liquid = tile.liquid;
                Main.tile[tile.X, tile.Y].skipLiquid = tile.skipLiquid;
                Main.tile[tile.X, tile.Y].type = tile.type;
                Main.tile[tile.X, tile.Y].wall = tile.wall;
                Main.tile[tile.X, tile.Y].wallFrameNumber = tile.wallFrameNumber;
                Main.tile[tile.X, tile.Y].wallFrameX = tile.wallFrameX;
                Main.tile[tile.X, tile.Y].wallFrameY = tile.wallFrameY;
                Main.tile[tile.X, tile.Y].wire = tile.wire;
                updateTile(tile.X, tile.Y);
            }
            player.tileList = new List<TileObj>();
        } 
        public static void updateTile(int x, int y)
        {

            x = Netplay.GetSectionX(x);
            y = Netplay.GetSectionY(y);
            foreach (Terraria.ServerSock theSock in Netplay.serverSock)
            {
                theSock.tileSection[x, y] = false;
            }

        }
        public static PlayerObj GetPlayerByID(int id)
        {
            foreach (PlayerObj ply in playerList)
            {
                if (ply.TSPlayer.UserID == id)
                    return ply;
            }
           // Console.WriteLine("returned new PlayerObj!");
            return new PlayerObj(-1);
        }  
    }

    public class TileObj 
    {
            public int X { get; set; }
            public int Y { get; set; }
            public bool active { get; set; }
            public bool checkingLiquid { get; set; }
            public TileData Data { get; set; }
            public byte frameNumber { get; set; }
            public short frameX { get; set; }
            public short frameY { get; set; }
            public bool lava { get; set; }
            public bool lighted { get; set; }
            public byte liquid { get; set; }
            public bool skipLiquid { get; set; }
            public byte type { get; set; }
            public byte wall { get; set; }
            public byte wallFrameNumber { get; set; }
            public byte wallFrameX { get; set; }
            public byte wallFrameY { get; set; }
            public bool wire { get; set; }
    }

    #region Player Class
    public class PlayerObj
    {
        public int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        public List<TileObj> tileList { get; set; }
        public bool AwaitingRegionName { get; set; }
        public PlayerObj(int id)
        {
            Index = id;
            tileList = new List<TileObj>();
        }
    }
    #endregion

    #region Region Class
    public class RegionObj
    {
        public Rectangle RegionArea { get; set; }
        public List<string> Owners { get; set; }
        public int ID { get; set; }
        public string Name { get; set; }
        public RegionObj(Rectangle area, List<string> owners, int id, string name)
        {
            RegionArea = area;
            Owners = owners;
            ID = id;
            Name = name;
        }
        public static bool AddOwner(string regName, string userName)
        {
            var reg = RegionTools.GetRegionByName(regName);
            StringBuilder sb = new StringBuilder();
            int count = 0;
            reg.Owners.Add(userName);
            foreach (string owner in reg.Owners)
            {
                count++;
                sb.Append(owner);
                if (count != reg.Owners.Count)
                    sb.Append(",");
            }
            List<SqlValue> values = new List<SqlValue>();
            values.Add(new SqlValue("Owners", "'" + sb.ToString() + "'"));
            List<SqlValue> wheres = new List<SqlValue>();
            wheres.Add(new SqlValue("Name", "'" + regName + "'"));
            AutoBanner.SQLEditor.UpdateValues("AutoBanRegions", values, wheres);
            return true;
        }
    }
    #endregion

    #region Region Tools
    public class RegionTools
    {
        public static bool NewRegion(int posX, int posY, int width, int height, string regionName, string owner)
        {
            List<SqlValue> values = new List<SqlValue>();
            values.Add(new SqlValue("Name", "'" + regionName + "'"));
            values.Add(new SqlValue("X", posX));
            values.Add(new SqlValue("Y", posY));
            values.Add(new SqlValue("Width", width));
            values.Add(new SqlValue("Height", height));
            values.Add(new SqlValue("Owners", "0"));
            AutoBanner.SQLEditor.InsertValues("AutoBanRegions", values);
            AutoBanner.regionList.Add(new RegionObj(new Rectangle(posX, posY, width, height), new List<string>(), (AutoBanner.regionList.Count + 1), regionName));
            return true;
        }
        public static RegionObj GetRegionByName(string name)
        {
            foreach (RegionObj reg in AutoBanner.regionList)
            {
                if (reg.Name == name)
                    return reg;
            }
            return null;
        }
        public static bool IsOwner(string UserID, string regionName)
        {
            var reg = RegionTools.GetRegionByName(regionName);
            foreach (string owner in reg.Owners)
            {
                if (owner == UserID)
                    return true;
            }
            return false;
        }
        public static string InAreaRegionName(int x, int y)
        {
            foreach (RegionObj reg in AutoBanner.regionList)
            {
                if (x >= reg.RegionArea.Left && x <= reg.RegionArea.Right && y >= reg.RegionArea.Top && y <= reg.RegionArea.Bottom)
                {
                    return reg.Name;
                }
            }
            return null;
        }
        public static bool resizeRegion(string regionName, int addAmount, int direction)
        {
            //0 = up
            //1 = right
            //2 = down
            //3 = left
            int X = 0;
            int Y = 0;
            int height = 0;
            int width = 0;
            try
            {
                using (var reader = TShock.DB.QueryReader("SELECT X, Y, Height, Width FROM AutoBanRegions WHERE Name=@0", regionName))
                {
                    if (reader.Read()) { X = reader.Get<int>("X"); }
                    width = reader.Get<int>("Width");
                    Y = reader.Get<int>("Y");
                    height = reader.Get<int>("Height");
                }
                if (!(direction == 0))
                {
                    if (!(direction == 1))
                    {
                        if (!(direction == 2))
                        {
                            if (!(direction == 3))
                            {
                                return false;
                            }
                            else
                            {
                                X -= addAmount;
                                width += addAmount;
                            }
                        }
                        else
                        {
                            height += addAmount;
                        }
                    }
                    else
                    {
                        width += addAmount;
                    }
                }
                else
                {
                    Y -= addAmount;
                    height += addAmount;
                }
                int q = TShock.DB.Query("UPDATE AutoBanRegions SET X = @0, Y = @1, Width = @2, Height = @3 WHERE Name = @4 ", X, Y, width, height, regionName);
                if (q > 0)
                    return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
            return false;
        }
        public static void ReadRegions()
        {
            AutoBanner.regionList = new List<RegionObj>();
            var reader = TShock.DB.QueryReader("Select * from AutoBanRegions");
            while (reader.Read())
            {
                int id = reader.Get<int>("ID");
                string[] list = reader.Get<string>("Owners").Split(',');
                List<string> owners = new List<string>();
                foreach (string i in list)
                    owners.Add(i);
                Rectangle regRect = new Rectangle(reader.Get<int>("X"), reader.Get<int>("Y"), reader.Get<int>("Width"), reader.Get<int>("Height"));
                AutoBanner.regionList.Add(new RegionObj(regRect, owners, id, reader.Get<string>("Name")));
            }
        }
    }
    #endregion

    #region Commands
    public class ABCommands
    {
        public static void ABCommand(CommandArgs args)
        {
            string cmd = "";
            if (args.Parameters.Count > 0)
            {
                cmd = args.Parameters[0].ToLower();
            }
            var player = AutoBanner.GetPlayerByID(args.Player.UserID);
            switch (cmd)
            {
                case "name":
                    {
                        if (args.Player.Group.HasPermission("abedit"))
                        {
                            args.Player.SendMessage("Hit a block to get the name of the AutoBan region", Color.Yellow);
                            player.AwaitingRegionName = true;
                        }
                        else
                        {
                            args.Player.SendMessage("You do not have access to that command.", Color.Red);
                        }
                        break;
                    }
                case "define":
                    {
                        if (args.Player.Group.HasPermission("abedit"))
                        {
                            if (args.Parameters.Count > 1)
                            {
                                if (!args.Player.TempPoints.Any(p => p == Point.Zero))
                                {
                                    string regionName = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
                                    var x = Math.Min(args.Player.TempPoints[0].X, args.Player.TempPoints[1].X);
                                    var y = Math.Min(args.Player.TempPoints[0].Y, args.Player.TempPoints[1].Y);
                                    var width = Math.Abs(args.Player.TempPoints[0].X - args.Player.TempPoints[1].X);
                                    var height = Math.Abs(args.Player.TempPoints[0].Y - args.Player.TempPoints[1].Y);
                                    if (RegionTools.NewRegion(x, y, width, height, regionName, args.Player.UserID.ToString()))
                                    {
                                        args.Player.TempPoints[0] = Point.Zero;
                                        args.Player.TempPoints[1] = Point.Zero;
                                        args.Player.SendMessage("Created AutoBan region: " + regionName, Color.Yellow);
                                        RegionObj.AddOwner(regionName, args.Player.UserID.ToString());
                                    }
                                    else
                                    {
                                        args.Player.SendMessage("AutoBan region " + regionName + " already exists", Color.Red);
                                    }
                                }
                                else
                                {
                                    args.Player.SendMessage("Points not set up yet", Color.Red);
                                }
                            }
                            else
                                args.Player.SendMessage("Invalid syntax! Proper syntax: /ab define RegionName", Color.Red);
                        }
                        else
                        {
                            args.Player.SendMessage("You do not have access to that command.", Color.Red);
                        }
                        break;
                    }

                case "resize":
                    {
                        if (args.Player.Group.HasPermission("abedit"))
                        {
                            if (args.Parameters.Count == 4)
                            {
                                int direction;
                                switch (args.Parameters[2])
                                {
                                    case "u":
                                    case "up":
                                        {
                                            direction = 0;
                                            break;
                                        }
                                    case "r":
                                    case "right":
                                        {
                                            direction = 1;
                                            break;
                                        }
                                    case "d":
                                    case "down":
                                        {
                                            direction = 2;
                                            break;
                                        }
                                    case "l":
                                    case "left":
                                        {
                                            direction = 3;
                                            break;
                                        }
                                    default:
                                        {
                                            direction = -1;
                                            break;
                                        }
                                }
                                int addAmount;
                                int.TryParse(args.Parameters[3], out addAmount);
                                if (RegionTools.resizeRegion(args.Parameters[1], addAmount, direction))
                                {
                                    args.Player.SendMessage("Region Resized Successfully!", Color.Yellow);
                                    RegionTools.ReadRegions();
                                }
                                else
                                {
                                    args.Player.SendMessage("Invalid syntax! Proper syntax: /ab resize [regionname] [u/d/l/r] [amount]",Color.Red);
                                }
                            }
                            else
                            {
                                args.Player.SendMessage("Invalid syntax! Proper syntax: /ab resize [regionname] [u/d/l/r] [amount]",Color.Red);
                            }
                            break;

                        }
                        else
                        {
                            args.Player.SendMessage("You do not have access to that command.", Color.Red);
                        }
                        break;
                    }
                case "allow":
                    {
                        if (args.Parameters.Count > 2)
                        {
                            string playerName = args.Parameters[1];
                            User playerID;
                            var reg = RegionTools.GetRegionByName(args.Parameters[2]);
                            string regionName = reg.Name;
                            if (RegionTools.IsOwner(args.Player.UserID.ToString(), reg.Name) || args.Player.Group.HasPermission("adminhouse") || args.Player.Group.Name == "superadmin")
                            {
                                if ((playerID = TShock.Users.GetUserByName(playerName)) != null)
                                {
                                    if (RegionObj.AddOwner(regionName, playerID.ID.ToString()))
                                    {
                                        args.Player.SendMessage("Added user " + playerName + " to " + regionName, Color.Yellow);
                                    }
                                    else
                                        args.Player.SendMessage("Region " + regionName + " not found", Color.Red);
                                }
                                else
                                {
                                    args.Player.SendMessage("Player " + playerName + " not found", Color.Red);
                                }
                            }
                            else
                            {
                                args.Player.SendMessage("You do not have permission to add users to: " + regionName);
                            }
                        }
                        else
                            args.Player.SendMessage("Invalid syntax! Proper syntax: /ab allow UserName RegionName", Color.Red);
                        break;
                    }
                case "delete":
                    {
                        if (args.Player.Group.HasPermission("abedit"))
                        {
                            if (args.Parameters.Count > 1)
                            {
                                string regionName = args.Parameters[1];
                                var reg = RegionTools.GetRegionByName(regionName);
                                if (RegionTools.IsOwner(args.Player.UserID.ToString(), reg.Name) || args.Player.Group.HasPermission("adminhouse") || args.Player.Group.Name == "superadmin")
                                {
                                    List<SqlValue> where = new List<SqlValue>();
                                    where.Add(new SqlValue("Name", "'" + regionName + "'"));
                                    AutoBanner.SQLWriter.DeleteRow("AutoBanRegions", where);
                                    AutoBanner.regionList.Remove(reg);
                                    args.Player.SendMessage("AutoBan region: " + regionName + " deleted", Color.Yellow);
                                    break;
                                }
                                else
                                {
                                    args.Player.SendMessage("You do not have permission to delete: " + regionName, Color.Yellow);
                                    break;
                                }
                            }
                            else
                                args.Player.SendMessage("Invalid syntax! Proper syntax: /ab delete RegionName", Color.Red);
                        }
                        else
                        {
                            args.Player.SendMessage("You do not have access to that command.", Color.Red);
                        }
                        break;
                    }
              /*  case "debug":
                    {
                        args.Player.SendMessage("tile[0] active: " + player.tileList[0].active + " tile[0] type: " + player.tileList[0].type);
                        break;
                    }*/
                default:
                    {
                        args.Player.SendMessage("AB commands available to you:", Color.Yellow);
                        if (args.Player.Group.HasPermission("abedit"))
                        {
                            args.Player.SendMessage("/ab name - gives you the name of the AutoBan region", Color.Yellow);
                            args.Player.SendMessage("/ab define RegionName - defines an AutoBan region between 2 points selected with /region set", Color.Yellow);
                            args.Player.SendMessage("/ab delete RegionName - deletes the specified AutoBan region", Color.Yellow);
                        }                        
                        args.Player.SendMessage("/ab allow UserName RegionName - allows the specified User to build/destroy in specified Region", Color.Yellow);
                        break;
                    }

            }
        }
    }
    #endregion

}
