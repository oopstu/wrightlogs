using System;

namespace WrightLogs.Models;

public static class QuoteGenerator
{
    private static readonly string[] allQuotes = [
        "If you think I’m wrong, I ain’t wrong.",
        "MEOW",
        "It’s always DNS. Or Memory.",
        "It’s 90% done.",
        "Happy 4th of July!",
        "Who is the Oracle?",
        "Bollywood Boss says “I don’t think so”",
        "Bollywood Boss says “Are you still hungry?”",
        "PS Candidate: “It’s a star!”",
        "I’m the new CEO!",
        "Sachin’s window…",
        "Have you checked the logs?",
        "What does the data say?  ",
        "We must be systematic…",
        "VISUALIZE YOUR WORK!"
    ];


    private static Random _random = new();
    internal static string GetQuote()
    {
       _random.Next(allQuotes.Length);
       return allQuotes[_random.Next(allQuotes.Length)]; 
    }
}