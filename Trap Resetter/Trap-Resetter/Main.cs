using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using MySql.Data.MySqlClient;

namespace TrapResetter
{
    
    [APIVersion(1, 11)]
    public class TrapResetter : TerrariaPlugin
    {
        private DateTime LastCheck = DateTime.UtcNow;
        public static Dictionary<int, byte> awaitingPlayers = new Dictionary<int, byte>();
        public static List<TrapObj> trapList = new List<TrapObj>();
        public static SqlTableEditor SQLEditor;
        public static SqlTableCreator SQLWriter;

        public override string Name
        {
            get { return "Trap Resetter"; }
        }
        public override string Author
        {
            get { return "by InanZen"; }
        }
        public override string Description
        {
            get { return "Replaces Boulders and Explosives"; }
        }
        public override Version Version
        {
            get { return new Version("1.0"); }
        }
        public override void Initialize()
        {  
            GameHooks.Initialize += OnInitialize;
            GameHooks.Update += OnUpdate;
            GetDataHandlers.TileEdit += TileEdit;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Initialize -= OnInitialize;
                GameHooks.Update -= OnUpdate;
                GetDataHandlers.TileEdit -= TileEdit;
            }
            base.Dispose(disposing);
        }
        public TrapResetter(Main game)
            : base(game)
        {
            Order = -1;
        }
        public void OnInitialize()
        {
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            var table = new SqlTable("TrapResetter",
                 new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                 new SqlColumn("X", MySqlDbType.Int32),
                 new SqlColumn("Y", MySqlDbType.Int32),
                 new SqlColumn("Type", MySqlDbType.Int32)
             );
            SQLWriter.EnsureExists(table);
            ReloadTraps();
            Commands.ChatCommands.Add(new Command("TrapReset", TrapCommand, "trap"));
            //Commands.ChatCommands.Add(new Command("TrapReset", BoulderCannon, "boulder"));
        }
        public static void ReloadTraps()
        {
            trapList = new List<TrapObj>();
            var reader = TShock.DB.QueryReader("Select * from TrapResetter");
            while (reader.Read())
            {
                trapList.Add(new TrapObj(reader.Get<int>("X"), reader.Get<int>("Y"), reader.Get<int>("Type")));
            }
        }
        public static bool AddTrap(int posX, int posY, int type)
        {
            List<SqlValue> values = new List<SqlValue>();
            values.Add(new SqlValue("X", posX));
            values.Add(new SqlValue("Y", posY));
            values.Add(new SqlValue("Type", type));
            SQLEditor.InsertValues("TrapResetter", values);
            trapList.Add(new TrapObj(posX, posY, type));
            return true;
        }
        public static bool DelTrap(int posX, int posY)
        {
            TrapObj trap = GetTrapByPos(posX, posY);
            if (trap.TrapID != 0)
            {
                List<SqlValue> where = new List<SqlValue>();
                where.Add(new SqlValue("X", posX));
                where.Add(new SqlValue("Y", posY));
                SQLWriter.DeleteRow("TrapResetter", where);
                trapList.Remove(trap);
                return true;    
            }
            return false;
        }
        public static TrapObj GetTrapByPos(int posX, int posY)
        {
            foreach (TrapObj trap in trapList)
            {
                if (trap.X == posX && trap.Y == posY)
                    return trap;
            }
            return new TrapObj(0,0,0);
        }
        void TileEdit(Object sender, TShockAPI.GetDataHandlers.TileEditEventArgs args)
        {
                    
          /* Console.WriteLine("Data: "+Main.tile[args.X,args.Y].Data.active+" type:"+Main.tile[args.X,args.Y].Data.type+" wall:"+Main.tile[args.X,args.Y].Data.wall+" -x:"+Main.tile[args.X,args.Y].Data.frameX+" y:"+Main.tile[args.X,args.Y].Data.frameY);
            Console.WriteLine("Active: " + Main.tile[args.X, args.Y].active + " checkLiq: " + Main.tile[args.X, args.Y].checkingLiquid + " framenum: " + Main.tile[args.X, args.Y].frameNumber + " framex:" + Main.tile[args.X, args.Y].frameX + " framey:" + Main.tile[args.X, args.Y].frameY);
            Console.WriteLine("lava: " + Main.tile[args.X, args.Y].lava + " lighted:" + Main.tile[args.X, args.Y].lighted + " liquid:" + Main.tile[args.X, args.Y].liquid + " skipLiq.:" + Main.tile[args.X, args.Y].skipLiquid + " type:" + Main.tile[args.X, args.Y].type);
            Console.WriteLine("wall: " + Main.tile[args.X, args.Y].wall + " wallframenum:" + Main.tile[args.X, args.Y].wallFrameNumber + " wallframeX:" + Main.tile[args.X, args.Y].wallFrameX + " wallframeY:" + Main.tile[args.X, args.Y].wallFrameY + " wire:" + Main.tile[args.X, args.Y].wire);
*/
            if (awaitingPlayers.Count > 0 && awaitingPlayers.Keys.Contains(args.Player.UserID))
            {
                byte awaitingType = awaitingPlayers[args.Player.UserID];
                if (awaitingType == 1)
                {
                    generateBoulder(args.X, args.Y);
                    args.Handled = true;
                    updateTile(args.X, args.Y);
                    if (AddTrap(args.X, args.Y, 138))
                        args.Player.SendMessage("Added Boulder Trap to selected location");
                    else
                        args.Player.SendMessage("Failed to add new trap", Color.Red);
                }
                else if (awaitingType == 2)
                {
                    generateExplosives(args.X, args.Y);
                    args.Handled = true;
                    updateTile(args.X, args.Y);
                    if (AddTrap(args.X, args.Y, 141))
                        args.Player.SendMessage("Added Explosives Trap to selected location");
                    else
                        args.Player.SendMessage("Failed to add new trap", Color.Red);
                }
                else
                {
                    if (DelTrap(args.X,args.Y))
                        args.Player.SendMessage("Removed Trap Reset from selected location");
                    else
                        args.Player.SendMessage("Failed to remove trap", Color.Red);

                }
                awaitingPlayers.Remove(args.Player.UserID);
            }
            /*else if (boulderCannonPlayers.Count > 0 && boulderCannonPlayers.Contains(args.Player.UserID))
            {
                generateBoulder(args.X, args.Y);
                updateTile(args.X, args.Y);
                args.Player.SendTileSquare(args.X, args.Y, 1);
            }
            */            
        }

        public void OnUpdate()
        {
            if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 10)
            {
                LastCheck = DateTime.UtcNow;
                foreach (TrapObj trap in trapList)
                {
                    if (Main.tile[trap.X, trap.Y].type != trap.TrapID)
                    {
                        if (trap.TrapID == 138) generateBoulder(trap.X, trap.Y);
                        else generateExplosives(trap.X, trap.Y);
                        updateTile(trap.X, trap.Y);
                        //Console.WriteLine("replaced trap");
                    }
                }
            }
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
        public void generateExplosives(int x, int y)
        {
            Main.tile[x, y].active = true;
            Main.tile[x, y].type = 141;
            Main.tile[x, y].frameX = 0;
            Main.tile[x, y].frameY = 18;
        }
        public void generateBoulder(int x, int y)
        {
            Main.tile[x, y].active = true;
            Main.tile[x, y].type = 138;
            Main.tile[x, y].frameX = 0;
            Main.tile[x, y].frameY = 18;

            Main.tile[x + 1, y].active = true;
            Main.tile[x + 1, y].type = 138;
            Main.tile[x + 1, y].frameX = 18;
            Main.tile[x + 1, y].frameY = 18;

            Main.tile[x, y - 1].active = true;
            Main.tile[x, y - 1].type = 138;
            Main.tile[x, y - 1].frameX = 0;
            Main.tile[x, y - 1].frameY = 0;

            Main.tile[x + 1, y - 1].active = true;
            Main.tile[x + 1, y - 1].type = 138;
            Main.tile[x + 1, y - 1].frameX = 18;
            Main.tile[x + 1, y - 1].frameY = 0;

        }

        public class TrapObj 
        {
            public int TrapID { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public TrapObj(int posX, int posY, int type)
            {
                TrapID = type;
                X = posX;
                Y = posY;
            }
        }
        public void TrapCommand(CommandArgs args)
        {
            string cmd = "";
            if (args.Parameters.Count > 0)
            {
                cmd = args.Parameters[0].ToLower();
            }
            switch (cmd)
            {
                case "boulder":
                    {
                        awaitingPlayers.Add(args.Player.UserID, 1);
                        args.Player.SendMessage("Mark the lower left corner of the Boulder position",Color.Violet);
                        break;
                    }
                case "explosives":
                    {
                        awaitingPlayers.Add(args.Player.UserID, 2);
                        args.Player.SendMessage("Hit a block to place Explosives", Color.Violet);
                        break;
                    }
                case "remove":
                    {
                        awaitingPlayers.Add(args.Player.UserID, 3);
                        args.Player.SendMessage("Hit a trap to remove it. For boulder lower-left corner!", Color.Violet);
                        break;
                    }
                default:
                    {
                        args.Player.SendMessage("Available commands:",Color.Violet);
                        args.Player.SendMessage("/trap boulder", Color.Violet);
                        args.Player.SendMessage("/trap explosives", Color.Violet);
                        args.Player.SendMessage("/trap remove", Color.Violet);
                        break;
                    }
            }
        }
       /* public void BoulderCannon(CommandArgs args)
        {
            if (boulderCannonPlayers.Contains(args.Player.UserID))
                boulderCannonPlayers.Remove(args.Player.UserID);
            else
                boulderCannonPlayers.Add(args.Player.UserID);
        }*/
    }
}
