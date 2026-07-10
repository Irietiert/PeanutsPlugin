using System.Collections.Generic;

namespace PeanutsPlugin.Data;

/// <summary>
/// Statische Zuordnung Weltname -> Datenzentrum, für die Gruppierung in der
/// Übersicht. Es werden ohnehin nur Welten angezeigt, die im Overlay bereits
/// mindestens einen gescannten Charakter haben (Configuration.Worlds enthält
/// nur besuchte Welten) - ungenutzte Datenzentren/Welten tauchen dadurch
/// automatisch nirgends auf, ganz ohne zusätzlichen Filter.
/// </summary>
public static class DataCenters
{
    private static readonly Dictionary<string, string> WorldToDataCenter = new()
    {
        // Europe
        ["Cerberus"] = "Chaos", ["Louisoix"] = "Chaos", ["Moogle"] = "Chaos", ["Omega"] = "Chaos",
        ["Phantom"] = "Chaos", ["Ragnarok"] = "Chaos", ["Sagittarius"] = "Chaos", ["Spriggan"] = "Chaos",

        ["Alpha"] = "Light", ["Lich"] = "Light", ["Odin"] = "Light", ["Phoenix"] = "Light",
        ["Raiden"] = "Light", ["Shiva"] = "Light", ["Twintania"] = "Light", ["Zodiark"] = "Light",

        // North America
        ["Adamantoise"] = "Aether", ["Cactuar"] = "Aether", ["Faerie"] = "Aether", ["Gilgamesh"] = "Aether",
        ["Jenova"] = "Aether", ["Midgardsormr"] = "Aether", ["Sargatanas"] = "Aether", ["Siren"] = "Aether",

        ["Balmung"] = "Crystal", ["Brynhildr"] = "Crystal", ["Coeurl"] = "Crystal", ["Diabolos"] = "Crystal",
        ["Goblin"] = "Crystal", ["Malboro"] = "Crystal", ["Mateus"] = "Crystal", ["Zalera"] = "Crystal",

        ["Cuchulainn"] = "Dynamis", ["Golem"] = "Dynamis", ["Halicarnassus"] = "Dynamis", ["Kraken"] = "Dynamis",
        ["Maduin"] = "Dynamis", ["Marilith"] = "Dynamis", ["Rafflesia"] = "Dynamis", ["Seraph"] = "Dynamis",

        ["Behemoth"] = "Primal", ["Excalibur"] = "Primal", ["Exodus"] = "Primal", ["Famfrit"] = "Primal",
        ["Hyperion"] = "Primal", ["Lamia"] = "Primal", ["Leviathan"] = "Primal", ["Ultros"] = "Primal",

        // Japan
        ["Aegis"] = "Elemental", ["Atomos"] = "Elemental", ["Carbuncle"] = "Elemental", ["Garuda"] = "Elemental",
        ["Gungnir"] = "Elemental", ["Kujata"] = "Elemental", ["Tonberry"] = "Elemental", ["Typhon"] = "Elemental",

        ["Alexander"] = "Gaia", ["Bahamut"] = "Gaia", ["Durandal"] = "Gaia", ["Fenrir"] = "Gaia",
        ["Ifrit"] = "Gaia", ["Ridill"] = "Gaia", ["Tiamat"] = "Gaia", ["Ultima"] = "Gaia",

        ["Anima"] = "Mana", ["Asura"] = "Mana", ["Chocobo"] = "Mana", ["Hades"] = "Mana",
        ["Ixion"] = "Mana", ["Masamune"] = "Mana", ["Pandaemonium"] = "Mana", ["Titan"] = "Mana",

        ["Belias"] = "Meteor", ["Mandragora"] = "Meteor", ["Ramuh"] = "Meteor", ["Shinryu"] = "Meteor",
        ["Unicorn"] = "Meteor", ["Valefor"] = "Meteor", ["Yojimbo"] = "Meteor", ["Zeromus"] = "Meteor",

        // Oceania
        ["Bismarck"] = "Materia", ["Ravana"] = "Materia", ["Sephirot"] = "Materia",
        ["Sophia"] = "Materia", ["Zurvan"] = "Materia",
    };

    /// <summary>Liefert das Datenzentrum zu einer Welt, oder "Unbekannt"/"Unknown" falls nicht in der Liste.</summary>
    public static string GetDataCenter(string worldName) =>
        WorldToDataCenter.TryGetValue(worldName, out var dc) ? dc : Loc.Get("Unbekannt", "Unknown");
}
