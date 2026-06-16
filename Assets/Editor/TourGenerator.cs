using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using YARG;
using YARG.Core.Song;
using YARG.Song;

namespace Editor
{
    public static class TourGenerator
    {
        private const string ToursFolder = @"C:\Users\sprim\Documents\Clone Hero\Tours";

        private const int SongsPerShow = 5;
        private const int ShowCount = 3;

        // ─── Existing sample tour ─────────────────────────────────────────────

        [MenuItem("YARG/Generate Sample Tour")]
        public static void GenerateSampleTour()
        {
            if (!EnsurePlayMode()) return;

            var allSongs = SongContainer.Songs;
            if (!EnsureSongsLoaded(allSongs)) return;

            var rng = new System.Random();
            int totalNeeded = SongsPerShow * ShowCount;
            var picked = allSongs
                .OrderBy(_ => rng.Next())
                .Take(totalNeeded)
                .ToArray();

            var shows = new TourShowData[ShowCount];
            for (int i = 0; i < ShowCount; i++)
            {
                var showSongs = picked
                    .Skip(i * SongsPerShow)
                    .Take(SongsPerShow)
                    .Select(s => new TourSongEntry
                    {
                        SongName = s.Name.Original,
                        Artist = s.Artist.Original,
                    })
                    .ToArray();

                shows[i] = new TourShowData
                {
                    ShowName = $"Show {i + 1}",
                    Songs = showSongs,
                    UnlockConditions = BuildUnlockConditions(i),
                };
            }

            var tour = new TourData
            {
                TourId = Guid.NewGuid(),
                TourName = "Sample Tour",
                Author = "YARG",
                Shows = shows,
            };

            SaveTour(tour, "sample_tour.tour");
            EditorUtility.DisplayDialog("Done", $"Sample tour saved to:\n{Path.Combine(ToursFolder, "sample_tour.tour")}", "OK");
        }

        /// <summary>
        /// Builds example unlock conditions for each show index.
        /// Show 0 is always unlocked (empty conditions).
        /// Show 1 requires 3 total songs completed.
        /// Show N (N>=2) requires 3*N total songs completed, plus 5 total stars.
        /// </summary>
        private static TourUnlockCondition[] BuildUnlockConditions(int showIndex)
        {
            return showIndex switch
            {
                0 => Array.Empty<TourUnlockCondition>(),
                1 => new[]
                {
                    new TourUnlockCondition
                    {
                        TypeOfCondition = TourUnlockCondition.ConditionType.SongsCompleted,
                        RequiredAmount  = 3,
                    },
                },
                _ => new[]
                {
                    new TourUnlockCondition
                    {
                        TypeOfCondition = TourUnlockCondition.ConditionType.SongsCompleted,
                        RequiredAmount  = 3 * showIndex,
                    },
                    new TourUnlockCondition
                    {
                        TypeOfCondition = TourUnlockCondition.ConditionType.StarsEarned,
                        RequiredAmount  = 5,
                    },
                },
            };
        }

        // ─── Classic game tours ────────────────────────────────────────────────

        [MenuItem("YARG/Tours/Guitar Hero 2 (360)")]
        public static void GenerateGH2Tour()
        {
            if (!EnsurePlayMode()) return;
            var allSongs = SongContainer.Songs;
            if (!EnsureSongsLoaded(allSongs)) return;

            var warnings = new List<string>();
            var tour = BuildGH2Tour(allSongs, warnings);
            SaveTour(tour, "guitar_hero_2.tour");
            ShowTourResult(tour.TourName, "guitar_hero_2.tour", warnings);
        }

        [MenuItem("YARG/Tours/Rock Band 1")]
        public static void GenerateRockBand1Tour()
        {
            if (!EnsurePlayMode()) return;
            var allSongs = SongContainer.Songs;
            if (!EnsureSongsLoaded(allSongs)) return;

            var warnings = new List<string>();
            var tour = BuildRockBand1Tour(allSongs, warnings);
            SaveTour(tour, "rock_band_1.tour");
            ShowTourResult(tour.TourName, "rock_band_1.tour", warnings);
        }

        [MenuItem("YARG/Tours/Rock Band 2")]
        public static void GenerateRockBand2Tour()
        {
            if (!EnsurePlayMode()) return;
            var allSongs = SongContainer.Songs;
            if (!EnsureSongsLoaded(allSongs)) return;

            var warnings = new List<string>();
            var tour = BuildRockBand2Tour(allSongs, warnings);
            SaveTour(tour, "rock_band_2.tour");
            ShowTourResult(tour.TourName, "rock_band_2.tour", warnings);
        }

        [MenuItem("YARG/Tours/Rock Band 3")]
        public static void GenerateRockBand3Tour()
        {
            if (!EnsurePlayMode()) return;
            var allSongs = SongContainer.Songs;
            if (!EnsureSongsLoaded(allSongs)) return;

            var warnings = new List<string>();
            var tour = BuildRockBand3Tour(allSongs, warnings);
            SaveTour(tour, "rock_band_3.tour");
            ShowTourResult(tour.TourName, "rock_band_3.tour", warnings);
        }

        [MenuItem("YARG/Tours/Green Day Rock Band")]
        public static void GenerateGreenDayTour()
        {
            if (!EnsurePlayMode()) return;
            var allSongs = SongContainer.Songs;
            if (!EnsureSongsLoaded(allSongs)) return;

            var warnings = new List<string>();
            var tour = BuildGreenDayTour(allSongs, warnings);
            SaveTour(tour, "green_day_rock_band.tour");
            ShowTourResult(tour.TourName, "green_day_rock_band.tour", warnings);
        }

        [MenuItem("YARG/Tours/Beatles Rock Band")]
        public static void GenerateBeatlesTour()
        {
            if (!EnsurePlayMode()) return;
            var allSongs = SongContainer.Songs;
            if (!EnsureSongsLoaded(allSongs)) return;

            var warnings = new List<string>();
            var tour = BuildBeatlesTour(allSongs, warnings);
            SaveTour(tour, "beatles_rock_band.tour");
            ShowTourResult(tour.TourName, "beatles_rock_band.tour", warnings);
        }

        [MenuItem("YARG/Tours/Generate All Classic Tours")]
        public static void GenerateAllClassicTours()
        {
            if (!EnsurePlayMode()) return;
            var allSongs = SongContainer.Songs;
            if (!EnsureSongsLoaded(allSongs)) return;

            var allWarnings = new List<string>();

            void Generate(Func<SongEntry[], List<string>, TourData> builder, string filename)
            {
                var w = new List<string>();
                var tour = builder(allSongs, w);
                SaveTour(tour, filename);
                foreach (var warning in w)
                    allWarnings.Add($"[{tour.TourName}] {warning}");
            }

            Generate(BuildGH2Tour, "guitar_hero_2.tour");
            Generate(BuildRockBand1Tour, "rock_band_1.tour");
            Generate(BuildRockBand2Tour, "rock_band_2.tour");
            Generate(BuildRockBand3Tour, "rock_band_3.tour");
            Generate(BuildGreenDayTour,  "green_day_rock_band.tour");
            Generate(BuildBeatlesTour,    "beatles_rock_band.tour");

            string header = "Generated 6 classic tours.\n\n";
            string body = allWarnings.Count == 0
                ? "All songs validated successfully — no missing songs detected."
                : $"WARNING: {allWarnings.Count} song(s) not found in the song cache.\n" +
                  "These may be misnamed in the hardcoded data or simply not in your library:\n\n" +
                  string.Join("\n", allWarnings.Take(40)) +
                  (allWarnings.Count > 40 ? $"\n... and {allWarnings.Count - 40} more." : string.Empty);

            EditorUtility.DisplayDialog("Classic Tours Generated", header + body, "OK");
            Debug.Log($"[TourGenerator] All classic tours saved to {ToursFolder}");
        }

        // ─── Tour builders ────────────────────────────────────────────────────

        /// <summary>
        /// Guitar Hero 2 — Xbox 360 version career mode.
        /// 8 tiers, each with a main show (5 songs) and an encore show (1 song).
        /// Unlocks: complete all main-show songs to unlock the tier encore;
        /// complete the encore to unlock the next tier.
        /// </summary>
        private static TourData BuildGH2Tour(SongEntry[] allSongs, List<string> warnings)
        {
            // Show index layout (main=M, encore=E):
            // 0:M1  1:E1  2:M2  3:E2  4:M3  5:E3  6:M4  7:E4
            // 8:M5  9:E5  10:M6 11:E6 12:M7 13:E7 14:M8 15:E8
            TourUnlockCondition[] Always() => Array.Empty<TourUnlockCondition>();
            // Cumulative: each tier = 5 main + 1 encore = 6 songs.
            // Main of tier k unlocks after encore of tier k-1: needs 6*k total songs.
            // Encore of tier k unlocks after main of tier k: needs 6*k+5 total songs.
            TourUnlockCondition[] AfterEncore(int encoreShowIdx) =>
                new[] { Completed(6 * ((encoreShowIdx + 1) / 2)) };
            TourUnlockCondition[] AfterMain(int mainShowIdx, int songCount) =>
                new[] { Completed(6 * (mainShowIdx / 2) + songCount) };

            var shows = new[]
            {
                // ── Tier 1: Opening Licks ──────────────────────────────────────
                Show("Opening Licks", Always(), allSongs, warnings,
                    Song("Salvation",           "Rancid"),
                    Song("Strutter",             "Kiss"),
                    Song("Surrender",            "Cheap Trick"),
                    Song("Possum Kingdom",       "Toadies"),
                    Song("Heart-Shaped Box",     "Nirvana")),

                Show("Opening Licks — Encore", AfterMain(0, 5), allSongs, warnings,
                    Song("Shout at the Devil",   "Mötley Crüe")),

                // ── Tier 2: Amp Warmers ────────────────────────────────────────
                Show("Amp Warmers", AfterEncore(1), allSongs, warnings,
                    Song("Woman",                    "Wolfmother"),
                    Song("Life Wasted",              "Pearl Jam"),
                    Song("Cherry Pie",               "Warrant"),
                    Song("Mother",                   "Danzig"),
                    Song("You Really Got Me",        "Van Halen")),

                Show("Amp Warmers — Encore", AfterMain(2, 5), allSongs, warnings,
                    Song("Tonight I'm Gonna Rock You Tonight", "Spinal Tap")),

                // ── Tier 3: String-Snappers ────────────────────────────────────
                Show("String-Snappers", AfterEncore(3), allSongs, warnings,
                    Song("Message in a Bottle",      "The Police"),
                    Song("Carry On Wayward Son",     "Kansas"),
                    Song("Billion Dollar Babies",    "Alice Cooper"),
                    Song("Search and Destroy",       "Iggy Pop and the Stooges"),
                    Song("Them Bones",               "Alice in Chains")),

                Show("String-Snappers — Encore", AfterMain(4, 5), allSongs, warnings,
                    Song("War Pigs",                 "Black Sabbath")),

                // ── Tier 4: Thrash and Burn ────────────────────────────────────
                Show("Thrash and Burn", AfterEncore(5), allSongs, warnings,
                    Song("Monkey Wrench",                  "Foo Fighters"),
                    Song("Hush",                           "Deep Purple"),
                    Song("Who Was in My Room Last Night?", "The Butthole Surfers"),
                    Song("Girlfriend",                     "Matthew Sweet"),
                    Song("Can't You Hear Me Knockin'",     "The Rolling Stones")),

                Show("Thrash and Burn — Encore", AfterMain(6, 5), allSongs, warnings,
                    Song("Sweet Child o' Mine",      "Guns N' Roses")),

                // ── Tier 5: Return of the Shred ────────────────────────────────
                Show("Return of the Shred", AfterEncore(7), allSongs, warnings,
                    Song("Rock and Roll Hoochie Koo", "Rick Derringer"),
                    Song("John the Fisherman",        "Primus"),
                    Song("Bad Reputation",            "Thin Lizzy"),
                    Song("Tattooed Love Boys",        "The Pretenders"),
                    Song("Jessica",                   "The Allman Brothers Band")),

                Show("Return of the Shred — Encore", AfterMain(8, 5), allSongs, warnings,
                    Song("Last Child",               "Aerosmith")),

                // ── Tier 6: Relentless Riffs ───────────────────────────────────
                Show("Relentless Riffs", AfterEncore(9), allSongs, warnings,
                    Song("Freya",                              "The Sword"),
                    Song("Dead!",                             "My Chemical Romance"),
                    Song("Trippin' on a Hole in a Paper Heart", "Stone Temple Pilots"),
                    Song("Killing in the Name",               "Rage Against the Machine"),
                    Song("Crazy on You",                      "Heart")),

                Show("Relentless Riffs — Encore", AfterMain(10, 5), allSongs, warnings,
                    Song("Stop!",                    "Jane's Addiction")),

                // ── Tier 7: Furious Fretwork ───────────────────────────────────
                Show("Furious Fretwork", AfterEncore(11), allSongs, warnings,
                    Song("Rock This Town",           "Stray Cats"),
                    Song("Psychobilly Freakout",     "Reverend Horton Heat"),
                    Song("Madhouse",                 "Anthrax"),
                    Song("Laid to Rest",             "Lamb of God"),
                    Song("The Trooper",              "Iron Maiden")),

                Show("Furious Fretwork — Encore", AfterMain(12, 5), allSongs, warnings,
                    Song("YYZ",                      "Rush")),

                // ── Tier 8: Face-Melters ───────────────────────────────────────
                Show("Face-Melters", AfterEncore(13), allSongs, warnings,
                    Song("Carry Me Home",            "The Living End"),
                    Song("Institutionalized",        "Suicidal Tendencies"),
                    Song("Hangar 18",                "Megadeth"),
                    Song("Misirlou",                 "Dick Dale"),
                    Song("Beast and the Harlot",     "Avenged Sevenfold")),

                Show("Face-Melters — Encore", AfterMain(14, 5), allSongs, warnings,
                    Song("Free Bird",                "Lynyrd Skynyrd")),
            };

            return new TourData
            {
                TourId = Guid.NewGuid(),
                TourName = "Guitar Hero 2",
                Author = "Harmonix / RedOctane",
                Shows = shows,
            };
        }

        /// <summary>
        /// Rock Band 1 — Solo Tour career mode.
        /// 9 tiers of 5 songs each, ordered by difficulty.
        /// Complete all 5 songs in a tier to unlock the next.
        /// </summary>
        private static TourData BuildRockBand1Tour(SongEntry[] allSongs, List<string> warnings)
        {
            TourUnlockCondition[] Always() => Array.Empty<TourUnlockCondition>();
            // Cumulative: each tier has 5 songs, so tier N requires 5*N total songs.
            TourUnlockCondition[] After(int showIdx) => new[] { Completed(5 * (showIdx + 1)) };

            var shows = new[]
            {
                Show("Warm-Up", Always(), allSongs, warnings,
                    Song("Here It Goes Again",   "OK Go"),
                    Song("In Bloom",             "Nirvana"),
                    Song("Learn to Fly",         "Foo Fighters"),
                    Song("Maps",                 "Yeah Yeah Yeahs"),
                    Song("Say It Ain't So",      "Weezer")),

                Show("Tier 2", After(0), allSongs, warnings,
                    Song("Are You Gonna Be My Girl", "Jet"),
                    Song("Blitzkrieg Bop",           "Ramones"),
                    Song("Main Offender",            "The Hives"),
                    Song("Next to You",              "The Police"),
                    Song("Orange Crush",             "R.E.M.")),

                Show("Tier 3", After(1), allSongs, warnings,
                    Song("Ballroom Blitz",            "Sweet (WaveGroup)"),
                    Song("Dead on Arrival",           "Fall Out Boy"),
                    Song("Reptilia",                  "The Strokes"),
                    Song("Should I Stay or Should I Go", "The Clash"),
                    Song("Suffragette City",          "David Bowie")),

                Show("Tier 4", After(2), allSongs, warnings,
                    Song("Black Hole Sun",       "Soundgarden"),
                    Song("Celebrity Skin",       "Hole"),
                    Song("Creep",                "Radiohead"),
                    Song("Dani California",      "Red Hot Chili Peppers"),
                    Song("I Think I'm Paranoid", "Garbage")),

                Show("Tier 5", After(3), allSongs, warnings,
                    Song("Electric Version",    "The New Pornographers"),
                    Song("Epic",                "Faith No More"),
                    Song("Flirtin' with Disaster", "Molly Hatchet"),
                    Song("Go with the Flow",    "Queens of the Stone Age"),
                    Song("Mississippi Queen",   "Mountain (WaveGroup)")),

                Show("Tier 6", After(4), allSongs, warnings,
                    Song("Foreplay / Long Time",   "Boston"),
                    Song("Gimme Shelter",        "The Rolling Stones"),
                    Song("Sabotage",             "Beastie Boys"),
                    Song("The Hand That Feeds",  "Nine Inch Nails"),
                    Song("When You Were Young",  "The Killers")),

                Show("Tier 7", After(5), allSongs, warnings,
                    Song("(Don't Fear) The Reaper", "Blue Öyster Cult"),
                    Song("Cherub Rock",              "The Smashing Pumpkins"),
                    Song("Detroit Rock City",        "Kiss"),
                    Song("Green Grass and High Tides", "The Outlaws (WaveGroup)"),
                    Song("Welcome Home",             "Coheed and Cambria")),

                Show("Tier 8", After(6), allSongs, warnings,
                    Song("Enter Sandman",    "Metallica"),
                    Song("Highway Star",     "Deep Purple"),
                    Song("Paranoid",         "Black Sabbath (WaveGroup)"),
                    Song("Run to the Hills", "Iron Maiden (WaveGroup)"),
                    Song("Vasoline",         "Stone Temple Pilots")),

                Show("Tier 9 — Finale", After(7), allSongs, warnings,
                    Song("Tom Sawyer",           "Rush (WaveGroup)"),
                    Song("Train Kept A-Rollin'", "Aerosmith (WaveGroup)"),
                    Song("Wanted Dead or Alive", "Bon Jovi"),
                    Song("Wave of Mutilation",   "Pixies"),
                    Song("Won't Get Fooled Again", "The Who")),
            };

            return new TourData
            {
                TourId = Guid.NewGuid(),
                TourName = "Rock Band 1",
                Author = "Harmonix",
                Shows = shows,
            };
        }

        /// <summary>
        /// Rock Band 2 — full 84-song disc setlist organised into 6 shows by
        /// difficulty/genre, reflecting the game's open-world Band World Tour.
        /// Each show requires completing a minimum number of songs in the previous
        /// show before it unlocks (approximating the fan-based venue gate).
        /// </summary>
        private static TourData BuildRockBand2Tour(SongEntry[] allSongs, List<string> warnings)
        {
            TourUnlockCondition[] Always() => Array.Empty<TourUnlockCondition>();
            TourUnlockCondition[] After(int totalCount) => new[] { Completed(totalCount) };

            var shows = new[]
            {
                // ── Show 0: Kick Off (pop / new wave / indie) ──────────────────
                Show("Kick Off", Always(), allSongs, warnings,
                    Song("Alex Chilton",              "The Replacements"),
                    Song("Bad Reputation",            "Joan Jett"),
                    Song("Conventional Lover",        "Speck"),
                    Song("Cool for Cats",             "Squeeze"),
                    Song("E-Pro",                     "Beck"),
                    Song("Eye of the Tiger",          "Survivor"),
                    Song("Float On",                  "Modest Mouse"),
                    Song("Hello There",               "Cheap Trick"),
                    Song("Hungry Like the Wolf",      "Duran Duran"),
                    Song("Kids in America",           "The Muffs"),
                    Song("Livin' on a Prayer",        "Bon Jovi"),
                    Song("Lump",                      "The Presidents of the United States of America"),
                    Song("My Own Worst Enemy",        "Lit"),
                    Song("Nine in the Afternoon",     "Panic! at the Disco")),

                // ── Show 1: Classic Rock (60s–70s) ─────────────────────────────
                Show("Classic Rock", After(8), allSongs, warnings,
                    Song("A Jagged Gorgeous Winter",  "The Main Drag"),
                    Song("Alabama Getaway",           "Grateful Dead"),
                    Song("American Woman",            "The Guess Who"),
                    Song("Any Way You Want It",       "Journey"),
                    Song("Aqualung",                  "Jethro Tull"),
                    Song("Bodhisattva",               "Steely Dan"),
                    Song("Carry On Wayward Son",      "Kansas"),
                    Song("Go Your Own Way",           "Fleetwood Mac"),
                    Song("Pinball Wizard",            "The Who"),
                    Song("Psycho Killer",             "Talking Heads"),
                    Song("Pump It Up",                "Elvis Costello"),
                    Song("Ramblin' Man",              "The Allman Brothers Band"),
                    Song("Rock'n Me",                 "Steve Miller Band"),
                    Song("Shooting Star",             "Bad Company"),
                    Song("Spirit in the Sky",         "Norman Greenbaum"),
                    Song("Tangled Up in Blue",        "Bob Dylan")),

                // ── Show 2: Alternative Nation (90s alt / grunge) ──────────────
                Show("Alternative Nation", After(16), allSongs, warnings,
                    Song("Alive",                          "Pearl Jam"),
                    Song("Come Out and Play (Keep 'Em Separated)", "The Offspring"),
                    Song("De-Luxe",                        "Lush"),
                    Song("Drain You",                      "Nirvana"),
                    Song("Everlong",                       "Foo Fighters"),
                    Song("Feel the Pain",                  "Dinosaur Jr."),
                    Song("Give It Away",                   "Red Hot Chili Peppers"),
                    Song("I Was Wrong",                    "Social Distortion"),
                    Song("Lazy Eye",                       "Silversun Pickups"),
                    Song("Man in the Box",                 "Alice in Chains"),
                    Song("New Kid in School",              "The Donnas"),
                    Song("PDA",                            "Interpol"),
                    Song("Pretend We're Dead",             "L7"),
                    Song("Rebel Girl",                     "Bikini Kill"),
                    Song("Spoonman",                       "Soundgarden"),
                    Song("Today",                          "The Smashing Pumpkins"),
                    Song("Where'd You Go?",                "The Mighty Mighty Bosstones")),

                // ── Show 3: Hard Hitters (hard rock / punk / nu-metal) ─────────
                Show("Hard Hitters", After(26), allSongs, warnings,
                    Song("Almost Easy",               "Avenged Sevenfold"),
                    Song("Chop Suey",                "System of a Down"),
                    Song("Down with the Sickness",    "Disturbed"),
                    Song("Get Clean",                 "Anarchy Club"),
                    Song("Girl's Not Grey",           "AFI"),
                    Song("Give It All",               "Rise Against"),
                    Song("Let There Be Rock",         "AC/DC"),
                    Song("Master Exploder",           "Tenacious D"),
                    Song("Mountain Song",             "Jane's Addiction"),
                    Song("One Step Closer",           "Linkin Park"),
                    Song("One Way or Another",        "Blondie"),
                    Song("So What'cha Want",          "Beastie Boys"),
                    Song("Teen Age Riot",             "Sonic Youth"),
                    Song("Testify",                   "Rage Against the Machine"),
                    Song("That's What You Get",       "Paramore"),
                    Song("The Middle",                "Jimmy Eat World"),
                    Song("You Oughta Know",           "Alanis Morissette")),

                // ── Show 4: Metal Assault ──────────────────────────────────────
                Show("Metal Assault", After(36), allSongs, warnings,
                    Song("Ace of Spades '08",         "Motörhead"),
                    Song("Battery",                   "Metallica"),
                    Song("Colony of Birchmen",        "Mastodon"),
                    Song("Night Lies",                "Bang Camaro"),
                    Song("Our Truth",                 "Lacuna Coil"),
                    Song("Painkiller",                "Judas Priest"),
                    Song("Peace Sells",               "Megadeth"),
                    Song("Round and Round",           "Ratt"),
                    Song("Shackler's Revenge",        "Guns N' Roses"),
                    Song("Shoulder to the Plow",      "Breaking Wheel"),
                    Song("Souls of Black",            "Testament"),
                    Song("Visions",                   "Abnormality"),
                    Song("White Wedding (Part 1)",    "Billy Idol")),

                // ── Show 5: Face-Melters (prog / bonus / harder cuts) ──────────
                Show("Face-Melters", After(46), allSongs, warnings,
                    Song("Panic Attack",              "Dream Theater"),
                    Song("Rob the Prez-O-Dent",       "That Handsome Devil"),
                    Song("Supreme Girl",              "The Sterns"),
                    Song("The Trees (Vault Edition)", "Rush"),
                    Song("Uncontrollable Urge",       "Devo"),
                    Song("We Got the Beat",           "The Go-Go's"),
                    Song("Welcome to the Neighborhood", "Libyans")),
            };

            return new TourData
            {
                TourId = Guid.NewGuid(),
                TourName = "Rock Band 2",
                Author = "Harmonix",
                Shows = shows,
            };
        }

        /// <summary>
        /// Rock Band 3 — full 83-song disc setlist organised into 6 Road Challenge
        /// groups by genre/era. Each group requires earning a minimum number of stars
        /// in the previous group before unlocking (approximating the Road Challenges
        /// star-gate system).
        /// </summary>
        private static TourData BuildRockBand3Tour(SongEntry[] allSongs, List<string> warnings)
        {
            TourUnlockCondition[] Always() => Array.Empty<TourUnlockCondition>();
            TourUnlockCondition[] AfterStars(int totalStars) => new[] { Stars(totalStars) };

            var shows = new[]
            {
                // ── Show 0: Rising Stars (accessible, pop / classic rock) ───────
                Show("Rising Stars", Always(), allSongs, warnings,
                    Song("Centerfold",                         "The J. Geils Band"),
                    Song("Everybody Wants to Rule the World",  "Tears for Fears"),
                    Song("Fly Like an Eagle",                  "Steve Miller Band"),
                    Song("Get Free",                           "The Vines"),
                    Song("Good Vibrations (Live)",             "The Beach Boys"),
                    Song("Here I Go Again",                    "Whitesnake"),
                    Song("I Got You (I Feel Good)",            "James Brown"),
                    Song("I Love Rock N' Roll",                "Joan Jett & The Blackhearts"),
                    Song("I Wanna Be Sedated",                 "Ramones"),
                    Song("The Look",                           "Roxette"),
                    Song("Low Rider",                          "WAR"),
                    Song("Need You Tonight",                   "INXS"),
                    Song("Walk of Life",                       "Dire Straits"),
                    Song("Walking on the Sun",                 "Smash Mouth"),
                    Song("Whip It",                            "Devo")),

                // ── Show 1: Classic Legends (60s–70s rock) ─────────────────────
                Show("Classic Legends", AfterStars(25), allSongs, warnings,
                    Song("20th Century Boy",                    "T. Rex"),
                    Song("25 or 6 to 4",                       "Chicago"),
                    Song("Bohemian Rhapsody",                  "Queen"),
                    Song("Break on Through (To the Other Side)", "The Doors"),
                    Song("China Grove",                        "The Doobie Brothers"),
                    Song("Crosstown Traffic",                  "The Jimi Hendrix Experience"),
                    Song("Free Bird",                          "Lynyrd Skynyrd"),
                    Song("I Can See for Miles",                "The Who"),
                    Song("I Need to Know",                     "Tom Petty and the Heartbreakers"),
                    Song("Imagine",                            "John Lennon"),
                    Song("Radar Love",                         "Golden Earring"),
                    Song("Roundabout",                         "Yes"),
                    Song("Saturday Night's Alright for Fighting", "Elton John"),
                    Song("Smoke on the Water",                 "Deep Purple"),
                    Song("Space Oddity",                       "David Bowie"),
                    Song("Werewolves of London",               "Warren Zevon")),

                // ── Show 2: New Wave & Indie (80s–00s) ────────────────────────
                Show("New Wave & Indie", AfterStars(60), allSongs, warnings,
                    Song("Antibodies",                         "Poni Hoax"),
                    Song("Cold as Ice",                        "Foreigner"),
                    Song("Combat Baby",                        "Metric"),
                    Song("The Con",                            "Tegan and Sara"),
                    Song("Don't Stand So Close to Me",         "The Police"),
                    Song("Heart of Glass",                     "Blondie"),
                    Song("In a Big Country",                   "Big Country"),
                    Song("Just Like Heaven",                   "The Cure"),
                    Song("The Killing Moon",                   "Echo & the Bunnymen"),
                    Song("Lasso",                              "Phoenix"),
                    Song("Last Dance",                         "The Raveonettes"),
                    Song("Living in America",                  "The Sounds"),
                    Song("Rock Lobster",                       "The B-52's"),
                    Song("Stop Me If You Think You've Heard This One Before", "The Smiths"),
                    Song("Yoshimi Battles the Pink Robots Pt. 1", "The Flaming Lips")),

                // ── Show 3: Alternative Rock ──────────────────────────────────
                Show("Alternative Rock", AfterStars(90), allSongs, warnings,
                    Song("The Beautiful People",               "Marilyn Manson"),
                    Song("Been Caught Stealing",               "Jane's Addiction"),
                    Song("Dead End Friends",                   "Them Crooked Vultures"),
                    Song("The Hardest Button to Button",       "The White Stripes"),
                    Song("Hey Man Nice Shot",                  "Filter"),
                    Song("In the Meantime",                    "Spacehog"),
                    Song("Midlife Crisis",                     "Faith No More"),
                    Song("Misery Business",                    "Paramore"),
                    Song("No One Knows",                       "Queens of the Stone Age"),
                    Song("Oh My God",                          "Ida Maria"),
                    Song("One Armed Scissor",                  "At the Drive-In"),
                    Song("Plush",                              "Stone Temple Pilots"),
                    Song("Portions for Foxes",                 "Rilo Kiley"),
                    Song("Rehab",                              "Amy Winehouse"),
                    Song("Get Up, Stand Up",                   "Bob Marley and the Wailers")),

                // ── Show 4: Metal & Punk ───────────────────────────────────────
                Show("Metal & Punk", AfterStars(120), allSongs, warnings,
                    Song("Before I Forget",                    "Slipknot"),
                    Song("Beast and the Harlot",               "Avenged Sevenfold"),
                    Song("Caught in a Mosh",                   "Anthrax"),
                    Song("Crazy Train",                        "Ozzy Osbourne"),
                    Song("Don't Bury Me... I'm Still Not Dead", "Riverboat Gamblers"),
                    Song("Du Hast",                            "Rammstein"),
                    Song("False Alarm",                        "The Bronx"),
                    Song("Foolin'",                            "Def Leppard"),
                    Song("Jerry Was a Race Car Driver",        "Primus"),
                    Song("King George",                        "Dover"),
                    Song("Llama",                              "Phish"),
                    Song("Rainbow in the Dark",                "Dio"),
                    Song("Sister Christian",                   "Night Ranger"),
                    Song("This Bastard's Life",                "Swingin' Utters")),

                // ── Show 5: Road Challenge Finals ─────────────────────────────
                Show("Road Challenge Finals", AfterStars(150), allSongs, warnings,
                    Song("Humanoid",                           "Tokio Hotel"),
                    Song("Killing Loneliness",                 "HIM"),
                    Song("Me Enamora",                         "Juanes"),
                    Song("Outer Space",                        "The Muffs"),
                    Song("Oye Mi Amor",                        "Maná"),
                    Song("The Power of Love",                  "Huey Lewis and the News"),
                    Song("Something Bigger, Something Brighter", "Pretty Girls Make Graves"),
                    Song("Viva la Resistance",                 "Hypernova")),
            };

            return new TourData
            {
                TourId = Guid.NewGuid(),
                TourName = "Rock Band 3",
                Author = "Harmonix",
                Shows = shows,
            };
        }

        /// <summary>
        /// Green Day: Rock Band — career organised into 3 venues with 3–4 sets each,
        /// plus a DLC bonus show. Unlocks sequentially: complete all songs in a set
        /// to unlock the next set. All songs are by Green Day.
        /// </summary>
        private static TourData BuildGreenDayTour(SongEntry[] allSongs, List<string> warnings)
        {
            TourUnlockCondition[] Always() => Array.Empty<TourUnlockCondition>();
            TourUnlockCondition[] After(int totalCount) => new[] { Completed(totalCount) };

            var shows = new[]
            {
                // ── Venue 1: The Warehouse — Dookie (1994) ────────────────────
                Show("The Warehouse: Set 1 (Dookie)", Always(), allSongs, warnings,
                    Song("F.O.D.",                "Green Day"),
                    Song("Having a Blast",        "Green Day"),
                    Song("Pulling Teeth",         "Green Day"),
                    Song("She",                   "Green Day"),
                    Song("When I Come Around",    "Green Day")),

                Show("The Warehouse: Set 2 (Dookie)", After(5), allSongs, warnings,
                    Song("Basket Case",           "Green Day"),
                    Song("Coming Clean",          "Green Day"),
                    Song("Emenius Sleepus",       "Green Day"),
                    Song("Sassafrass Roots",       "Green Day"),
                    Song("Welcome to Paradise",   "Green Day")),

                Show("The Warehouse: Set 3 (Dookie)", After(10), allSongs, warnings,
                    Song("Burnout",               "Green Day"),
                    Song("Chump",                 "Green Day"),
                    Song("In the End",            "Green Day"),
                    Song("Longview",              "Green Day")),

                // ── Venue 2: Milton Keynes National Bowl — 2005 ───────────────
                Show("Milton Keynes: Set 1", After(14), allSongs, warnings,
                    Song("Boulevard of Broken Dreams",      "Green Day"),
                    Song("Extraordinary Girl",              "Green Day"),
                    Song("Geek Stink Breath",               "Green Day"),
                    Song("Wake Me Up When September Ends",  "Green Day"),
                    Song("Warning",                         "Green Day")),

                Show("Milton Keynes: Set 2 (American Idiot)", After(19), allSongs, warnings,
                    Song("American Idiot",        "Green Day"),
                    Song("Minority",              "Green Day"),
                    Song("Nice Guys Finish Last", "Green Day"),
                    Song("Whatsername",           "Green Day")),

                Show("Milton Keynes: Set 3 (American Idiot)", After(23), allSongs, warnings,
                    Song("Give Me Novacaine/She's a Rebel", "Green Day"),
                    Song("Hitchin' a Ride",                 "Green Day"),
                    Song("Holiday",                         "Green Day"),
                    Song("Jesus of Suburbia",               "Green Day"),
                    Song("Letterbomb",                      "Green Day")),

                Show("Milton Keynes: Set 4 (American Idiot)", After(28), allSongs, warnings,
                    Song("Are We the Waiting/St. Jimmy",    "Green Day"),
                    Song("Brain Stew/Jaded",                "Green Day"),
                    Song("Good Riddance (Time of Your Life)", "Green Day"),
                    Song("Homecoming",                      "Green Day")),

                // ── Venue 3: The Fox Theater, Oakland — 21st Century Breakdown ─
                Show("Fox Theater: Set 1 (21st Century Breakdown)", After(32), allSongs, warnings,
                    Song("Last Night on Earth",              "Green Day"),
                    Song("Restless Heart Syndrome",         "Green Day"),
                    Song("Song of the Century",             "Green Day"),
                    Song("¿Viva La Gloria? (Little Girl)",  "Green Day")),

                Show("Fox Theater: Set 2 (21st Century Breakdown)", After(36), allSongs, warnings,
                    Song("American Eulogy",       "Green Day"),
                    Song("Before the Lobotomy",   "Green Day"),
                    Song("Murder City",           "Green Day"),
                    Song("See the Light",         "Green Day")),

                Show("Fox Theater: Set 3 (21st Century Breakdown)", After(40), allSongs, warnings,
                    Song("21st Century Breakdown",        "Green Day"),
                    Song("Horseshoes and Handgrenades",   "Green Day"),
                    Song("Peacemaker",                    "Green Day"),
                    Song("The Static Age",                "Green Day")),

                // ── Bonus DLC ─────────────────────────────────────────────────
                Show("Bonus DLC", After(44), allSongs, warnings,
                    Song("21 Guns",                   "Green Day"),
                    Song("East Jesus Nowhere",        "Green Day"),
                    Song("Know Your Enemy",           "Green Day"),
                    Song("Christian's Inferno",       "Green Day"),
                    Song("Last of the American Girls", "Green Day"),
                    Song("¡Viva La Gloria!",          "Green Day")),
            };

            return new TourData
            {
                TourId = Guid.NewGuid(),
                TourName = "Green Day: Rock Band",
                Author = "Harmonix / MTV Games",
                Shows = shows,
            };
        }

        /// <summary>
        /// The Beatles: Rock Band — Story Mode, strictly linear through 8 chapters
        /// in chronological order (1963–1969). Complete all songs in a chapter to
        /// unlock the next. The Rooftop Concert encore ("The End") is its own show
        /// unlocked after completing the final chapter.
        /// </summary>
        private static TourData BuildBeatlesTour(SongEntry[] allSongs, List<string> warnings)
        {
            TourUnlockCondition[] Always() => Array.Empty<TourUnlockCondition>();
            TourUnlockCondition[] After(int totalCount) => new[] { Completed(totalCount) };

            var shows = new[]
            {
                // ── Chapter 1: The Cavern Club, Liverpool (1963) ───────────────
                Show("The Cavern Club", Always(), allSongs, warnings,
                    Song("I Saw Her Standing There",  "The Beatles"),
                    Song("Boys",                      "The Beatles"),
                    Song("Do You Want to Know a Secret", "The Beatles"),
                    Song("Twist and Shout",           "The Beatles")),

                // ── Chapter 2: The Ed Sullivan Theater, New York (1964) ────────
                Show("The Ed Sullivan Theater", After(4), allSongs, warnings,
                    Song("I Want to Hold Your Hand",  "The Beatles"),
                    Song("I Wanna Be Your Man",        "The Beatles"),
                    Song("Can't Buy Me Love",          "The Beatles"),
                    Song("A Hard Day's Night",         "The Beatles")),

                // ── Chapter 3: Shea Stadium, New York (1965) ──────────────────
                Show("Shea Stadium", After(8), allSongs, warnings,
                    Song("Ticket to Ride",            "The Beatles"),
                    Song("Eight Days a Week",         "The Beatles"),
                    Song("I Feel Fine",               "The Beatles"),
                    Song("I'm Looking Through You",   "The Beatles"),
                    Song("If I Needed Someone",       "The Beatles")),

                // ── Chapter 4: Budokan, Tokyo (1966) ──────────────────────────
                Show("Budokan", After(13), allSongs, warnings,
                    Song("Paperback Writer",          "The Beatles"),
                    Song("Drive My Car",              "The Beatles"),
                    Song("Taxman",                    "The Beatles"),
                    Song("Day Tripper",               "The Beatles"),
                    Song("And Your Bird Can Sing",    "The Beatles")),

                // ── Chapter 5: Abbey Road Studios '66–'67 (Dreamscape) ────────
                Show("Abbey Road Studios '66-'67", After(18), allSongs, warnings,
                    Song("Yellow Submarine",          "The Beatles"),
                    Song("Within You Without You / Tomorrow Never Knows", "The Beatles"),
                    Song("Lucy in the Sky with Diamonds", "The Beatles"),
                    Song("Getting Better",            "The Beatles"),
                    Song("Good Morning Good Morning", "The Beatles"),
                    Song("Sgt. Pepper's Lonely Hearts Club Band / With a Little Help from My Friends", "The Beatles")),

                // ── Chapter 6: Abbey Road Studios '67–'68 (Dreamscape) ────────
                Show("Abbey Road Studios '67-'68", After(24), allSongs, warnings,
                    Song("I Am the Walrus",           "The Beatles"),
                    Song("Hello Goodbye",            "The Beatles"),
                    Song("Hey Bulldog",               "The Beatles"),
                    Song("Dear Prudence",             "The Beatles"),
                    Song("Back in the U.S.S.R.",      "The Beatles"),
                    Song("While My Guitar Gently Weeps", "The Beatles")),

                // ── Chapter 7: Abbey Road Studios '68–'69 (Dreamscape) ────────
                Show("Abbey Road Studios '68-'69", After(30), allSongs, warnings,
                    Song("Birthday",                  "The Beatles"),
                    Song("Helter Skelter",            "The Beatles"),
                    Song("Revolution",                "The Beatles"),
                    Song("Octopus's Garden",          "The Beatles"),
                    Song("Here Comes the Sun",        "The Beatles"),
                    Song("Something",                 "The Beatles"),
                    Song("Come Together",             "The Beatles")),

                // ── Chapter 8: The Rooftop Concert, London (1969) ─────────────
                Show("The Rooftop Concert", After(37), allSongs, warnings,
                    Song("Don't Let Me Down",         "The Beatles"),
                    Song("I've Got a Feeling",        "The Beatles"),
                    Song("Dig a Pony",                "The Beatles"),
                    Song("I Me Mine",                 "The Beatles"),
                    Song("Get Back",                  "The Beatles"),
                    Song("I Want You (She's So Heavy)", "The Beatles")),

                // ── Encore: Career Finale ─────────────────────────────────────
                Show("Encore — The End", After(43), allSongs, warnings,
                    Song("The End",                   "The Beatles")),
            };

            return new TourData
            {
                TourId   = Guid.NewGuid(),
                TourName = "The Beatles: Rock Band",
                Author   = "Harmonix / Apple Corps",
                Shows    = shows,
            };
        }

        // ─── Shared utilities ─────────────────────────────────────────────────

        private static TourShowData Show(
            string name,
            TourUnlockCondition[] conditions,
            SongEntry[] allSongs,
            List<string> warnings,
            params TourSongEntry[] songs)
        {
            foreach (var song in songs)
                ValidateSong(song, name, allSongs, warnings);

            return new TourShowData
            {
                ShowName = name,
                Songs = songs,
                UnlockConditions = conditions,
            };
        }

        private static TourSongEntry Song(string name, string artist) =>
            new TourSongEntry { SongName = name, Artist = artist };

        private static TourUnlockCondition Completed(int count) =>
            new TourUnlockCondition
            {
                TypeOfCondition = TourUnlockCondition.ConditionType.SongsCompleted,
                RequiredAmount = count,
            };

        private static TourUnlockCondition Stars(int count) =>
            new TourUnlockCondition
            {
                TypeOfCondition = TourUnlockCondition.ConditionType.StarsEarned,
                RequiredAmount = count,
            };

        private static void ValidateSong(
            TourSongEntry entry,
            string showName,
            SongEntry[] allSongs,
            List<string> warnings)
        {
            bool found = Array.Exists(allSongs, s =>
                string.Equals(s.Name.Original, entry.SongName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.Artist.Original, entry.Artist, StringComparison.OrdinalIgnoreCase));

            if (!found)
                warnings.Add($"  [{showName}] \"{entry.SongName}\" — {entry.Artist}");
        }

        private static void SaveTour(TourData tour, string filename)
        {
            Directory.CreateDirectory(ToursFolder);

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
            };

            var path = Path.Combine(ToursFolder, filename);
            if (File.Exists(path))
            {
                try
                {
                    var existing = JsonConvert.DeserializeObject<TourData>(File.ReadAllText(path), settings);
                    if (existing != null && existing.TourId != Guid.Empty)
                    {
                        tour.TourId = existing.TourId;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TourGenerator] Could not read existing tour ID from {path}: {e.Message}");
                }
            }

            var json = JsonConvert.SerializeObject(tour, settings);
            File.WriteAllText(path, json);
            Debug.Log($"[TourGenerator] Saved tour to {path}");
        }

        private static bool EnsurePlayMode()
        {
            if (Application.isPlaying) return true;

            EditorUtility.DisplayDialog(
                "Play Mode Required",
                "The game must be running in Play mode to generate tours, " +
                "so that the song cache is populated.",
                "OK");
            return false;
        }

        private static bool EnsureSongsLoaded(SongEntry[] songs)
        {
            if (songs.Length > 0) return true;

            EditorUtility.DisplayDialog(
                "No Songs Loaded",
                "No songs are loaded in SongContainer. " +
                "Make sure song folders are configured and the cache has been scanned.",
                "OK");
            return false;
        }

        private static void ShowTourResult(string tourName, string filename, List<string> warnings)
        {
            string path = Path.Combine(ToursFolder, filename);

            if (warnings.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    $"{tourName} — Done",
                    $"Tour saved to:\n{path}\n\nAll songs validated successfully.",
                    "OK");
            }
            else
            {
                string missingList = string.Join("\n", warnings.Take(30)) +
                    (warnings.Count > 30 ? $"\n... and {warnings.Count - 30} more." : string.Empty);

                EditorUtility.DisplayDialog(
                    $"{tourName} — Saved with Warnings",
                    $"Tour saved to:\n{path}\n\n" +
                    $"WARNING: {warnings.Count} song(s) were not found in the song cache.\n" +
                    "They may be misnamed in the hardcoded data or not present in your library:\n\n" +
                    missingList,
                    "OK");
            }
        }
    }
}
