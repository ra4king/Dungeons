using System;
using System.Collections.Generic;
using System.IO;
using InteractiveFiction;
using WorldFileParser;

namespace WorldFileBuilder
{
    class WorldBuilder
    {
        public static List<Room> build(Player player, List<Node> nodes)
        {
            List<Room> rooms = new List<Room>();

            string startingRoomName = null;

            foreach (Node n in nodes)
            {
                if (n is Pair)
                {
                    Pair p = (Pair)n;
                    if (p.name.Equals("StartingRoom", StringComparison.OrdinalIgnoreCase))
                        startingRoomName = p.value;
                    else if (p.name.Equals("Intro", StringComparison.OrdinalIgnoreCase))
                        player.intro = p.value.Replace("\\n","\n");
                    else
                        throw new InvalidDataException();
                }
                else
                {
                    Room r = buildRoom(player, n);
                    rooms.Add(r);
                    if (r.name.Equals(startingRoomName))
                        player.room = r;
                }
            }

            foreach (Room r in rooms)
                foreach (Wall w in r.getWalls())
                    if (w.door != null && w.door.name != null)
                        w.door.room = getRoom(player, rooms, w.door.name);

            return rooms;
        }

        private static Room getRoom(Player player, List<Room> rooms, string name)
        {
            foreach (Room r in rooms)
            {
                if (r.name == name)
                    return r;
            }

            throw new InvalidDataException(name + " isn't a valid room name.");
        }

        private static bool isDirection(string name)
        {
            try
            {
                Direction d = (Direction)Enum.Parse(typeof(Direction), name, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Room buildRoom(Player player, Node node)
        {
            Room room = new Room(node.name);

            foreach (Node n in node.children)
            {
                if (n is Pair && n.name.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    room.description = ((Pair)n).value;
                else if (n.name.Equals("Items", StringComparison.OrdinalIgnoreCase))
                    room.Items = buildItems(player, null, n);
            }
            
            buildWalls(player, room, node);

            return room;
        }

        private static void buildWalls(Player player, Room room, Node node)
        {
            foreach (Node n in node.children)
            {
                Direction d;

                try
                {
                    d = (Direction)Enum.Parse(typeof(Direction), n.name, true);
                }
                catch
                {
                    continue;
                }

                buildWall(player, room.getWall(d), n);
            }
        }

        private static void buildWall(Player player, Wall wall, Node node)
        {
            foreach (Node n in node.children)
            {
                if (n.name.Equals("Door", StringComparison.OrdinalIgnoreCase))
                    wall.door = buildDoor(wall, n);
            }

            wall.items = buildItems(player, wall, node);
        }

        private static Door buildDoor(Wall wall, Node node)
        {
            Door door;

            if (node is Pair)
                door = new Door(wall, ((Pair)node).value);
            else
            {
                door = new Door(wall, "");

                foreach (Node n in node.children)
                {
                    if (n is Pair)
                    {
                        Pair p = (Pair)n;

                        if (p.name.Equals("Target", StringComparison.OrdinalIgnoreCase))
                            door.name = p.value;
                        else if (p.name.Equals("Locked", StringComparison.OrdinalIgnoreCase))
                            door.locked = bool.Parse(p.value);
                        else if (p.name.Equals("Visible", StringComparison.OrdinalIgnoreCase))
                            door.visible = bool.Parse(p.value);
                        else if (p.name.Equals("Description", StringComparison.OrdinalIgnoreCase))
                            door.description = p.value;
                    }
                }
            }

            return door;
        }

        private static List<Item> buildItems(Player player, Wall wall, Node node)
        {
            List<Item> items = new List<Item>();

            foreach (Node n in node.children)
            {
                if (n.name.Equals("Door", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (n is Pair)
                    throw new InvalidDataException("Cannot have a free standing pair under Items");
                else
                    items.Add(buildItem(player, wall, n));
            }

            return items;
        }

        private static Item buildItem(Player player, Wall wall, Node node)
        {
            Item item = new Item(wall, node.name);

            bool hasDescription = false;

            List<string> commands = new List<string>();
            commands.Add("use");
            commands.Add("examine");

            foreach (Node n in node.children)
            {
                if (n is Pair)
                {
                    Pair p = (Pair)n;

                    if (p.name.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    {
                        item.description = p.value;
                        hasDescription = true;
                    }
                    else if (p.name.Equals("Takeable", StringComparison.OrdinalIgnoreCase))
                        item.takeable = bool.Parse(p.value);
                    else if (p.name.Equals("UseIfNotEquipped", StringComparison.OrdinalIgnoreCase))
                        item.useIfNotEquipped = bool.Parse(p.value);
                    else if (p.name.Equals("Visible", StringComparison.OrdinalIgnoreCase))
                        item.visible = bool.Parse(p.value);
                    else if (p.name.Equals("Detail", StringComparison.OrdinalIgnoreCase))
                        item.detail = p.value;
                    else if (p.name.Equals("Alias", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] aliases = p.value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string s in aliases)
                            item.addAlias(s.Trim());
                    }
                    else if (p.name.Equals("Commands", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] cs = p.value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string s in cs)
                            commands.Add(s.Trim());
                    }
                    else if (commands.Contains(p.name))
                    {
                        item.addCommand(p.name, createCommand(player, item, p.value, null));
                    }
                }
                else
                {
                    if (!n.name.Equals("Children", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Cannot have a node under an item except for Children.");

                    item.addAll(buildItems(player, wall, n));
                }
            }

            if (!hasDescription)
                throw new InvalidDataException("No description found in Item.");

            return item;
        }

        private static Command createCommand(Player player, Item origin, string value, Dictionary<string, Instruction> variables)
        {
            value = value.Replace("[", "").Replace("]", "").Trim();

            string[] cs = value.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (cs.Length == 0)
                throw new InvalidDataException("No data found for this command.");

            List<Instruction> instructions = new List<Instruction>();

            if (variables == null)
                variables = new Dictionary<string, Instruction>();

            foreach (string s in cs)
            {
                string c = s.Trim();

                if (c.StartsWith("print", StringComparison.OrdinalIgnoreCase))
                    instructions.Add(InstructionFactory.createPrint(c.Substring(c.IndexOf('-') + 1).Trim()));
                else if (c.StartsWith("remove", StringComparison.OrdinalIgnoreCase))
                    instructions.Add(InstructionFactory.createRemove());
                else if (c.StartsWith("drop", StringComparison.OrdinalIgnoreCase))
                    instructions.Add(InstructionFactory.createDrop());
                else if (c.StartsWith("take", StringComparison.OrdinalIgnoreCase))
                    instructions.Add(InstructionFactory.createTake());
                else if (c.StartsWith("GameOver", StringComparison.OrdinalIgnoreCase))
                    instructions.Add(InstructionFactory.createGameOver(bool.Parse(c.ToLower().Replace("gameover","").Trim())));
                else if (c.StartsWith("if", StringComparison.OrdinalIgnoreCase))
                {
                    int i = c.IndexOf("if");
                    int e = c.IndexOf("then");

                    string cond = c.Substring(i + 2, e - 1 - 2).Trim();

                    Instruction variable;
                    object v;

                    if (cond.Contains("is"))
                    {
                        string[] conds = cond.Split(new string[] { "is" }, StringSplitOptions.RemoveEmptyEntries);

                        if (conds.Length != 2)
                            throw new InvalidDataException("Invalid conditional statement.");

                        variable = InstructionFactory.createVariable(conds[1].Trim());

                        v = conds[0].Trim();
                    }
                    else
                    {
                        string[] conds = cond.Split(new string[] { "==" }, StringSplitOptions.RemoveEmptyEntries);

                        if (conds.Length != 2)
                            throw new InvalidDataException("Invalid conditional statement.");

                        try
                        {
                            v = int.Parse(conds[1].Trim());
                        }
                        catch
                        {
                            v = conds[1].Trim();
                        }

                        variable = variables[conds[0].Trim()];
                    }

                    try
                    {
                        instructions.Add(InstructionFactory.createConditional(variable, v, createCommand(player, origin, c.Substring(e + 4).Replace(" >", ":").Trim(), variables)));
                    }
                    catch (KeyNotFoundException ex)
                    {
                        throw new InvalidDataException("Invalid variable:" + cond[0]);
                    }
                }
                else if (c.StartsWith("var ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] var = c.Substring(4).Trim().Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                    variables.Add(var[0].Trim(), InstructionFactory.createVariable(int.Parse(var[1].Trim())));
                }
                else if (c.Contains("++"))
                {
                    c = c.Replace("++", "").Trim();

                    try
                    {
                        instructions.Add(InstructionFactory.incrementVariable(variables[c]));
                    }
                    catch (KeyNotFoundException e)
                    {
                        throw new InvalidDataException("Invalid variable:" + c);
                    }
                }
                else if (c.Contains("--"))
                {
                    c = c.Replace("--", "").Trim();

                    try
                    {
                        instructions.Add(InstructionFactory.decrementVariable(variables[c]));
                    }
                    catch (KeyNotFoundException e)
                    {
                        throw new InvalidDataException("Invalid variable:" + c);
                    }
                }
                else if (c.Contains("="))
                {
                    string[] assignments = c.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                    if (assignments.Length == 0)
                        throw new InvalidDataException("No data found for this instruction.");
                    if (assignments.Length == 1)
                        throw new InvalidDataException("Invalid instruction:" + c);
                    if (assignments.Length > 2)
                        throw new InvalidDataException("Too many values:" + c);

                    string[] item = assignments[0].Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

                    if (item.Length == 1)
                    {
                        try
                        {
                            instructions.Add(InstructionFactory.modifyVariable(variables[item[0].Trim()], int.Parse(assignments[1].Trim())));
                        }
                        catch (KeyNotFoundException e)
                        {
                            throw new InvalidDataException("Invalid variable:" + item[0]);
                        }
                    }
                    else if (item.Length < 3)
                        throw new InvalidDataException("Too little fields:" + assignments[0]);
                    else if (item.Length > 3)
                        throw new InvalidDataException("Too many field:" + assignments[0]);
                    else
                        instructions.Add(InstructionFactory.createAssignment(item[0].Trim() + '.' + item[1].Trim(), item[2].Trim(), bool.Parse(assignments[1].Trim())));
                }
                else
                {
                    throw new InvalidDataException("Invalid instruction:" + c);
                }
            }

            return str =>
            {
                Item target;

                if (str.IndexOf(" door", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    str = str.ToLower().Replace(" door", "");

                    Wall w = player.room.getWall(str);
                    if (w == null)
                    {
                        Console.WriteLine("'" + str + "' is not a valid direction.");
                        return;
                    }

                    if (w.door == null)
                    {
                        Console.WriteLine("There is no door here.");
                        return;
                    }

                    target = w.door;
                }
                else
                {
                    target = player.getItem(str);
                    if (target == null)
                        target = player.room.getItem(str);
                }

                try
                {
                    foreach (Instruction i in instructions)
                        i.execute(player, origin, target);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            };
        }
    }
}
