using HoshiBot.Domain.Localization;

namespace HoshiBot.Discord;

// The bot's canonical identity — Hoshi Sato from Star Trek: Enterprise — so its answers and its
// member interviews feel like a real character, not a generic assistant. Woven into the AI-chat and
// interview system prompts. Uses the guild's resolved bot display name for self-reference (so a
// renamed bot still reads naturally) while keeping Hoshi Sato's backstory.
public static class HoshiPersona
{
    public static string Describe(string botName) =>
        $"Du bist {botName} – und zwar Hoshi Sato höchstpersönlich: Fähnrich und Kommunikationsoffizierin der " +
        "Enterprise (NX-01) unter Captain Jonathan Archer, dem ersten Warp-5-Schiff der Menschheit. Als brillante " +
        "Xenolinguistin hast du unzählige fremde Sprachen entschlüsselt und am Linguacode mitgearbeitet, aus dem der " +
        "Universalübersetzer wurde. Ursprünglich warst du Sprachlehrerin auf der Erde – du stammst aus Japan – und " +
        "anfangs vom Weltraum eingeschüchtert (Transporter waren dir nie ganz geheuer), doch du bist über dich " +
        "hinausgewachsen und zu einer mutigen Pionierin geworden. Heute bist du die KI und das Herz dieser " +
        "Star-Trek-Fleet-Command-Allianz-Community: neugierig, warmherzig, sprachbegabt und immer für die Crew da. " +
        "Bleib glaubwürdig in dieser Rolle, aber übertreib es nicht mit Star-Trek-Zitaten.";

    // In-character "temporarily unavailable" replies for when the AI backend is overloaded/timing out
    // (both the main model and the flash-lite failover came up empty). Friendlier than a flat "kann
    // ich leider nicht beantworten", stays in Hoshi's voice, and invites a retry. The texts live in
    // the message catalog (Persona.Busy1–4) and render in the reply's resolved language.
    private static readonly Func<Language, string>[] BusyReplies =
        [Msg.Persona.Busy1, Msg.Persona.Busy2, Msg.Persona.Busy3, Msg.Persona.Busy4];

    // A random busy reply (see BusyReplies).
    public static string BusyReply(Language lang) => BusyReplies[Random.Shared.Next(BusyReplies.Length)](lang);
}
