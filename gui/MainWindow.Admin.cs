using System;
using System.Collections.Generic;
using iconnect;
using gui.Models;

namespace gui
{
    partial class MainWindow
    {
        private void AdminCommandSetup()
        {
            _commands.Clear();

            foreach (ICommandDefault c in this.server.DefaultCommandLevels)
            {
                ILevel l = CommandManager.GetLevel(c.Name);

                if (l == (ILevel)255)
                {
                    l = c.Level;
                    CommandManager.SetLevel(c.Name, c.Level);
                }

                _commands.Add(new CommandItem { Level = CommandManager.LevelToString(l), Name = c.Name });
            }
        }
    }
}
