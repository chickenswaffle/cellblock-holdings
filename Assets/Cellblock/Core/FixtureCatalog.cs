using System;
using System.Collections.Generic;

namespace Cellblock.Core
{
    // Dimensions are the complete occupied footprint, including integrated seating. Keep saved
    // kind IDs stable: labels/categories may change without rewriting a company's furnishings.
    public sealed class FixtureDefinition
    {
        public readonly string Id, Name, Category, Description;
        public readonly float Width, Depth, Height, AccessX, AccessY;
        public readonly long PriceCents;
        public FixtureDefinition(string id, string name, string category, string description,
            float width, float depth, float height, long priceCents, float accessX, float accessY)
        {
            Id = id; Name = name; Category = category; Description = description;
            Width = width; Depth = depth; Height = height; PriceCents = priceCents;
            AccessX = accessX; AccessY = accessY;
        }
    }

    public static class FixtureCatalog
    {
        static readonly FixtureDefinition[] entries = {
            new FixtureDefinition("Bed", "Single bed", "Housing", "Steel bed, mattress and underbed storage. Adds one bed after handover.", 1f, 2f, 0.80f, 45000, 0.8f, 0),
            new FixtureDefinition("Locker", "Personal locker", "Housing", "Ventilated steel locker for personal belongings and a tidier living space.", 0.62f, 0.62f, 1.82f, 28000, 0, -0.66f),
            new FixtureDefinition("Toilet", "Sanitary toilet", "Hygiene", "Floor mounted toilet with concealed cistern, seat and flush control.", 0.68f, 0.88f, 0.85f, 38000, 0, -0.79f),
            new FixtureDefinition("Sink", "Wash basin", "Hygiene", "Pedestal basin with a tap, drain and rear splash guard.", 0.72f, 0.60f, 1.10f, 24000, 0, -0.65f),
            new FixtureDefinition("Shower", "Shower station", "Hygiene", "Tiled shower tray, screened sides and a fixed shower head.", 1.0f, 1.0f, 2.18f, 62000, 0, -0.85f),
            new FixtureDefinition("Table", "Dining table", "Dining", "Four seat table with fixed benches. Can be designated as the meal service destination.", 1.9f, 2.2f, 1.0f, 65000, 1.25f, 0),
            new FixtureDefinition("Chair", "Dining chair", "Dining", "Individual steel framed chair with a wipe clean seat and supportive back.", 0.60f, 0.65f, 0.88f, 9500, 0, -0.68f),
            new FixtureDefinition("ServingCounter", "Serving counter", "Dining", "Stainless food service counter with inset trays, shelf and sneeze screen.", 2.0f, 0.85f, 1.55f, 115000, 0, -0.78f),
            new FixtureDefinition("Bench", "Common room bench", "Recreation", "Three seat bench for shared spaces, with a timber seat and steel frame.", 1.70f, 0.65f, 0.88f, 21000, 0, -0.68f),
            new FixtureDefinition("Bookshelf", "Library shelf", "Recreation", "Open shelving stocked with books for a reading or common room.", 0.95f, 0.42f, 1.74f, 32000, 0, -0.56f),
            new FixtureDefinition("Television", "Television cabinet", "Recreation", "A television on a secure low cabinet for a shared recreation space.", 1.15f, 0.52f, 1.42f, 48000, 0, -0.62f),
            new FixtureDefinition("ExerciseBench", "Exercise bench", "Recreation", "Padded exercise station with a supported bar and weight plates.", 1.10f, 1.95f, 1.35f, 73000, 0.90f, 0),
            new FixtureDefinition("Desk", "Administration desk", "Administration", "Office desk with monitor, paperwork, storage drawers and a tucked stool.", 1.9f, 1.2f, 1.35f, 65000, 0, -0.92f),
            new FixtureDefinition("FilingCabinet", "Records cabinet", "Administration", "Lockable four drawer cabinet for the administration workspace.", 0.58f, 0.66f, 1.35f, 26000, 0, -0.69f)
        };
        static readonly Dictionary<string, FixtureDefinition> byId = Index();
        public static IReadOnlyList<FixtureDefinition> All { get; } = Array.AsReadOnly(entries);
        public static IReadOnlyList<string> Categories { get; } = Array.AsReadOnly(new[] { "Housing", "Hygiene", "Dining", "Recreation", "Administration" });
        static Dictionary<string, FixtureDefinition> Index()
        {
            var result = new Dictionary<string, FixtureDefinition>(StringComparer.Ordinal);
            foreach (FixtureDefinition entry in entries) result.Add(entry.Id, entry);
            return result;
        }
        public static FixtureDefinition Get(string kind)
        {
            FixtureDefinition entry;
            return kind != null && byId.TryGetValue(kind, out entry) ? entry : null;
        }
        public static bool Contains(string kind) { return Get(kind) != null; }
    }
}
