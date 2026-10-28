namespace ArtificialLife.Core;

// Fictional names with a culture per faction: people (gendered first name + surname), officer titles (gendered,
// unique per world) and places (kind-aware: towns, ports, mines, lairs...). Names are built from syllables and roots,
// so each culture yields hundreds of thousands of combinations; real Portuguese words and real place names are blocked.
public enum NameCulture { Iron, Ash, Tide, Free, Veil, Road, Common }

public sealed class NameForge
{
    sealed record Phonology(string[] Onsets, string[] Vowels, string[] Codas, string[] MaleEndings, string[] FemaleEndings, string[] SurnameRoots, string[] SurnameSuffixes);

    // Each culture has its own sound: Iron (hard, northern), Ash (latin, liturgical), Tide (iberian, maritime),
    // Free (eastern, miners), Veil (dark, whispered), Road (desert caravans), Common (island villagers).
    static readonly Dictionary<NameCulture, Phonology> Cultures = new()
    {
        [NameCulture.Iron] = new(new[] { "br", "dr", "gr", "kr", "st", "th", "v", "w", "h", "r", "g", "k", "t", "b", "sk", "hald", "ul" }, new[] { "a", "e", "i", "o", "u", "ei", "au" },
            new[] { "", "n", "r", "rd", "lf", "lk", "rn", "st", "ld", "x" }, new[] { "", "ar", "ek", "olt", "in", "en", "rik", "ulf" }, new[] { "a", "ild", "ga", "run", "wen", "ina" },
            new[] { "Stahl", "Kron", "Hald", "Varn", "Eisen", "Grau", "Holt", "Brand", "Steg", "Wolk", "Rand", "Dorn", "Fels", "Hart", "Kalt" }, new[] { "mar", "berg", "wald", "heim", "dorf", "rik", "holm", "vard", "stein", "feld", "gard", "brun" }),
        [NameCulture.Ash] = new(new[] { "c", "l", "m", "s", "t", "v", "f", "pr", "cl", "fl", "gr", "qu", "d", "n", "r", "ser", "aur" }, new[] { "a", "e", "i", "o", "u", "ae", "io" },
            new[] { "", "l", "n", "s", "x", "r", "m" }, new[] { "us", "ius", "en", "an", "o", "ius", "ar" }, new[] { "a", "ia", "ina", "ella", "is", "ena" },
            new[] { "Cinér", "Lumen", "Sant", "Clar", "Vesper", "Aurel", "Penit", "Sever", "Pal", "Fest", "Ign", "Tac" }, new[] { "io", "ius", "ano", "ella", "ério", "ícia", "ino", "arro", "ente", "or" }),
        [NameCulture.Tide] = new(new[] { "b", "c", "d", "g", "j", "l", "m", "n", "p", "r", "s", "t", "v", "x", "z", "ch", "rr" }, new[] { "a", "e", "i", "o", "u", "ai", "ou" },
            new[] { "", "l", "n", "r", "s", "z" }, new[] { "o", "ão", "el", "ez", "ito", "ando" }, new[] { "a", "ela", "ina", "inha", "ada", "ira" },
            new[] { "Maré", "Sal", "Quilh", "Ventan", "Âncor", "Gávi", "Proa", "Rem", "Ostr", "Corsel", "Brum", "Escarl" }, new[] { "eira", "ado", "ino", "al", "osa", "ez", "ão", "ega", "ilho", "ana" }),
        [NameCulture.Free] = new(new[] { "b", "d", "g", "k", "l", "m", "n", "p", "r", "s", "t", "v", "z", "dr", "kr", "zh", "sv" }, new[] { "a", "e", "i", "o", "u", "ya", "yo" },
            new[] { "", "n", "r", "k", "v", "sh", "sk", "t" }, new[] { "an", "ek", "ov", "ir", "ko", "in", "osh" }, new[] { "a", "ya", "ka", "ina", "ena", "ova" },
            new[] { "Kamen", "Rud", "Zhelez", "Gor", "Vol", "Svob", "Brat", "Kol", "Mir", "Ugol", "Drug", "Lom" }, new[] { "ov", "ev", "in", "ski", "nik", "ets", "ar", "enko", "ich", "ak" }),
        [NameCulture.Veil] = new(new[] { "m", "n", "s", "v", "z", "th", "sh", "x", "l", "h", "nh", "ss", "mor", "val" }, new[] { "a", "e", "i", "o", "u", "y", "ae" },
            new[] { "", "th", "s", "x", "l", "n", "m" }, new[] { "ath", "on", "ys", "eth", "or", "il" }, new[] { "a", "ys", "ira", "elle", "eth", "ana" },
            new[] { "Umbr", "Morr", "Noct", "Sepul", "Vel", "Cinz", "Lamen", "Oss", "Funer", "Silen", "Obscur", "Crep" }, new[] { "ário", "eth", "is", "ário", "oz", "ane", "ith", "ous", "ell", "ax" }),
        [NameCulture.Road] = new(new[] { "b", "d", "f", "h", "j", "k", "m", "n", "q", "r", "s", "t", "z", "sh", "kh", "dh", "rh" }, new[] { "a", "e", "i", "o", "u", "aa", "ei" },
            new[] { "", "n", "r", "m", "d", "l", "z", "sh" }, new[] { "an", "ir", "ad", "im", "ur", "esh" }, new[] { "a", "ia", "ra", "eh", "ina", "aya" },
            new[] { "Dun", "Qar", "Sahr", "Tam", "Zar", "Bek", "Hal", "Mir", "Nas", "Raz", "Fal", "Kas" }, new[] { "ani", "ari", "eddin", "ush", "ira", "oud", "ah", "imi", "an", "esh" }),
        [NameCulture.Common] = new(new[] { "b", "c", "d", "f", "g", "j", "l", "m", "n", "p", "r", "s", "t", "v", "br", "cr", "tr" }, new[] { "a", "e", "i", "o", "u", "ia", "eo" },
            new[] { "", "l", "n", "r", "s" }, new[] { "o", "an", "el", "ino", "ério", "eu" }, new[] { "a", "ina", "ela", "ia", "ilde", "ana" },
            new[] { "Ribeir", "Mour", "Fontel", "Barr", "Varz", "Camp", "Serr", "Lagar", "Penh", "Tojal", "Carvalh", "Freix" }, new[] { "inho", "ão", "ela", "osa", "al", "edo", "eira", "ado", "ais", "ense" }),
    };

    // Common Portuguese words (and a few crude ones) that must never appear as a name or as part of one.
    static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "vovo", "vovó", "vivo", "viva", "magra", "magro", "tata", "riri", "lele", "nana", "papa", "mama", "bebe", "baba", "cocô", "coco", "pipi", "xixi", "caca", "cuca", "dodo",
        "bobo", "boba", "tolo", "tola", "feio", "feia", "gordo", "gorda", "mole", "pau", "cu", "puta", "puto", "merda", "bosta", "porra", "caralho", "cona", "rola", "pinto", "pica", "teta",
        "mala", "malo", "nada", "nado", "sala", "mesa", "casa", "gato", "gata", "rato", "rata", "pato", "pata", "vaca", "boi", "sapo", "mosca", "lixo", "sujo", "suja", "burro", "burra",
        "seca", "seco", "dor", "mar", "sol", "lua", "ano", "mão", "pé", "rei", "lei", "fim", "sim", "não", "fogo", "agua", "ar", "terra", "pedra", "osso", "ossos", "morte", "morto", "morta",
        "mina", "rio", "vale", "porto", "santa", "santo", "sao", "são", "boa", "bom", "mau", "má", "tia", "tio", "vila", "nina", "bia", "duda", "lulu",
    };

    // Real Brazilian/Portuguese place names a root+suffix could accidentally produce.
    static readonly HashSet<string> RealPlaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "Marília", "Pedreira", "Salvador", "Brumadinho", "Pinheiral", "Ventania", "Corvo", "Serrinha", "Ribeirão", "Mourão", "Campinas", "Barreiras", "Varzelândia", "Serrana",
        "Penha", "Lagarto", "Freixo", "Carvalhal", "Camposa", "Fontela", "Sertão", "Mariana", "Santarém", "Valença", "Viseu", "Braga", "Porto", "Lagos", "Faro", "Sines", "Tomar",
    };

    // Real words, names, surnames and places the root+suffix pairs of the cultures can produce (found by listing every
    // pair, 25/10/2026). Checked for surnames, given names and place names alike.
    static readonly HashSet<string> RealWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Iron (German)
        "Stahlberg", "Kronberg", "Kronstein", "Eisenberg", "Eisenstein", "Eisenheim", "Eisenach", "Hartberg", "Hartheim", "Hartstein", "Dornberg", "Dornfeld", "Dornheim",
        "Felsberg", "Brandberg", "Brandstein", "Holtdorf", "Graustein", "Randberg",
        // Ash (Latin)
        "Aurelio", "Aurelius", "Aurelino", "Severino", "Santino", "Clarino", "Palio", "Festino", "Penitente", "Santana",
        // Tide (Portuguese, maritime)
        "Brumado", "Brumal", "Brumosa", "Saleira", "Salado", "Salino", "Salão", "Salal", "Remado", "Ostreira", "Ventaneira", "Gávião", "Gavião",
        // Free (Slavic)
        "Kamenov", "Kamenev", "Kamenski", "Kamenets", "Rudin", "Rudnik", "Rudenko", "Gorin", "Gorski", "Gornik", "Volski", "Kolev", "Kolin", "Kolar", "Mirov", "Lomov",
        // Veil
        "Ossário", "Funerário", "Velário", "Morris", "Morrell", "Veloz",
        // Road (Arabic)
        "Tamari", "Tamimi", "Zarah", "Nasira", "Falah", "Razan", "Mirah", "Halimi", "Kasimi",
        // Common (Portuguese places and words)
        "Ribeirinho", "Mourinho", "Moureira", "Barrão", "Barrela", "Barrosa", "Barral", "Barredo", "Barreira", "Barrado", "Campinho", "Campal", "Campeira", "Serrão", "Serrado",
        "Penhedo", "Carvalhinho", "Carvalhosa", "Carvalheira", "Carvalhais", "Freixinho", "Freixosa", "Freixedo", "Lagareira",
    };
    static bool Real(string word) => RealWords.Contains(word) || RealPlaces.Contains(word);

    readonly HashSet<string> _usedPeople = new(StringComparer.OrdinalIgnoreCase), _usedTitles = new(StringComparer.OrdinalIgnoreCase), _usedPlaces = new(StringComparer.OrdinalIgnoreCase);

    internal string[][] ExportUsed() => new[] { _usedPeople.ToArray(), _usedTitles.ToArray(), _usedPlaces.ToArray() };
    internal void ImportUsed(string[][] used) { _usedPeople.UnionWith(used[0]); _usedTitles.UnionWith(used[1]); _usedPlaces.UnionWith(used[2]); }

    // Gendered epithets (male, female); a world never repeats one.
    static readonly (string Male, string Female)[] Titles =
    {
        ("o Lobo", "a Loba"), ("o Carniceiro", "a Carniceira"), ("o Quebra-ossos", "a Quebra-ossos"), ("o Sem-medo", "a Sem-medo"), ("o Justo", "a Justa"), ("o Cinzento", "a Cinzenta"),
        ("a Víbora", "a Víbora"), ("o Corvo", "a Corva"), ("o Silencioso", "a Silenciosa"), ("o Implacável", "a Implacável"), ("o Caolho", "a Caolha"), ("o Velho", "a Velha"),
        ("o Ferreiro", "a Ferreira"), ("o Sangrento", "a Sangrenta"), ("o Paciente", "a Paciente"), ("o Rápido", "a Rápida"), ("o Gigante", "a Gigante"), ("o Mudo", "a Muda"),
        ("o Profeta", "a Profetisa"), ("o Caçador", "a Caçadora"), ("o Errante", "a Errante"), ("o Frio", "a Fria"), ("o Marcado", "a Marcada"), ("o Teimoso", "a Teimosa"),
        ("o Lâmina", "a Lâmina"), ("o Tempestade", "a Tempestade"), ("o Urso", "a Ursa"), ("o Falcão", "a Falcoa"), ("o Penitente", "a Penitente"), ("o Mercador", "a Mercadora"),
        ("o Coveiro", "a Coveira"), ("o Enforcador", "a Enforcadora"), ("o Bravo", "a Brava"), ("o Astuto", "a Astuta"), ("o Pálido", "a Pálida"), ("o Ruivo", "a Ruiva"),
        ("o Escudo", "o Escudo"), ("o Martelo", "a Martela"), ("o Javali", "a Javalina"), ("o Sombrio", "a Sombria"),
    };

    public static NameCulture CultureFor(FactionProfile p) => p.Culture ?? (p.Monsters ? NameCulture.Common : p.Nomadic ? NameCulture.Road : (NameCulture)(p.Id % 5));

    /// <summary>Gendered first name + surname of the culture, unique within this forge.</summary>
    public string Person(NameCulture culture, bool female, ref DeterministicRandom random)
    {
        var ph = Cultures[culture];
        for (int attempt = 0; attempt < 200; attempt++)
        {
            string first = Capital(Word(ph, random.Next(2) + 1, female ? ph.FemaleEndings : ph.MaleEndings, ref random));
            string last = ph.SurnameRoots[random.Next(ph.SurnameRoots.Length)] + ph.SurnameSuffixes[random.Next(ph.SurnameSuffixes.Length)];
            string full = first + " " + last;
            if (first.Length < 3 || first.Length > 8 || !Pronounceable(first) || Blocked.Contains(first) || Blocked.Contains(last) || Real(first) || Real(last) || ContainsBlocked(first) || !_usedPeople.Add(full)) continue;
            return full;
        }
        return "Sem Nome " + _usedPeople.Count;
    }

    /// <summary>Epithet for an officer; never repeated in the same world.</summary>
    public string Title(bool female, ref DeterministicRandom random)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            var t = Titles[random.Next(Titles.Length)]; string title = female ? t.Female : t.Male;
            if (_usedTitles.Add(title)) return title;
        }
        return "";
    }

    /// <summary>Place name fitting its kind and owner culture (e.g. "Porto Quilhado", "Mina de Kamenets", "Ninho Umbráx").</summary>
    public string Place(string kind, NameCulture culture, ref DeterministicRandom random)
    {
        var ph = Cultures[culture];
        for (int attempt = 0; attempt < 200; attempt++)
        {
            string core = random.Next(3) == 0
                ? Capital(Word(ph, 2, ph.MaleEndings, ref random))
                : ph.SurnameRoots[random.Next(ph.SurnameRoots.Length)] + ph.SurnameSuffixes[random.Next(ph.SurnameSuffixes.Length)];
            if (Blocked.Contains(core) || Real(core) || core.Length < 4 || core.Length > 12 || !Pronounceable(core)) continue;
            string name = kind switch
            {
                "Port" => "Porto " + core, "Mine" => "Mina de " + core, "Fishing" => "Enseada " + core, "Farm" => "Herdade " + core,
                "Nest" => "Ninho de " + core, "Ruins" => "Ruínas de " + core, "Town" => core, _ => core,
            };
            if (_usedPlaces.Add(name)) return name;
        }
        return kind + " " + _usedPlaces.Count;
    }

    static string Word(Phonology ph, int syllables, string[] endings, ref DeterministicRandom random)
    {
        var s = new System.Text.StringBuilder();
        for (int i = 0; i < syllables; i++)
        {
            s.Append(ph.Onsets[random.Next(ph.Onsets.Length)]).Append(ph.Vowels[random.Next(ph.Vowels.Length)]);
            if (i < syllables - 1) s.Append(ph.Codas[random.Next(ph.Codas.Length)]);
        }
        s.Append(endings[random.Next(endings.Length)]);
        return s.ToString();
    }

    // Readable in Portuguese: no three vowels or three consonants in a row, no doubled vowel, no doubled odd consonant.
    static bool Pronounceable(string word)
    {
        const string vowels = "aeiouyáéíóúãõâêôà";
        string w = word.ToLowerInvariant(); int v = 0, c = 0;
        for (int i = 0; i < w.Length; i++)
        {
            bool vowel = vowels.IndexOf(w[i]) >= 0;
            if (vowel) { v++; c = 0; } else { c++; v = 0; }
            if (v >= 3 || c >= 3) return false;
            if (i > 0 && w[i] == w[i - 1] && (vowel || "rsl".IndexOf(w[i]) < 0)) return false;
        }
        return !w.StartsWith("ss") && !w.StartsWith("rr") && !w.StartsWith("nh");
    }

    static bool ContainsBlocked(string name) { string n = name.ToLowerInvariant(); foreach (var w in new[] { "puta", "merda", "bosta", "porra", "caralho", "cona", "pica", "rola" }) if (n.Contains(w)) return true; return false; }
    static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    /// <summary>Approximate number of distinct full names a culture can produce (both genders).</summary>
    public static long Combinations(NameCulture culture)
    {
        var ph = Cultures[culture];
        long syl = ph.Onsets.Length * ph.Vowels.Length, firsts = syl * (ph.MaleEndings.Length + ph.FemaleEndings.Length) + syl * ph.Codas.Length * syl * (ph.MaleEndings.Length + ph.FemaleEndings.Length);
        return firsts * ph.SurnameRoots.Length * ph.SurnameSuffixes.Length;
    }
}
