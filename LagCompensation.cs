using System;
using static NoClippy.NoClippy;

namespace NoClippy
{
    public static class LagCompensation
    {
        // ALL INFO BELOW IS BASED ON MY FINDINGS AND I RESERVE THE RIGHT TO HAVE MISINTERPRETED SOMETHING, THANKS
        // The typical time range that passes for the client is never equal to ping, it always seems to be at least ping + server delay
        // The server delay is usually around 40-60 ms in the overworld, but falls to 30-40 ms inside of instances
        // Additionally, your FPS will add more time because one frame MUST pass for you to receive the new animation lock
        // Therefore, most players will never receive a response within 40 ms at any ping
        // Another interesting fact is that the delay from the server will spike if you send multiple packets at the same time
        // This seems to imply that the server will not process more than one packet from you per tick
        // You can see this if you sheathe your weapon before using an ability, you will notice delays that are around 50 ms higher than usual
        // This explains the phenomenon where moving seems to make it harder to weave

        // For these reasons, I do not believe it is possible to triple weave on any ping without clipping even the slightest amount as that would require 25 ms response times for a 2.5 gcd triple

        // Simulates around 10 ms ping
        private const float MinSimDelay = 0.04f;
        private const float MaxSimDelay = 0.06f;


        private static byte ignoreNext = 0;
        private static float delay = -1;
        private static float simDelay = (MaxSimDelay - MinSimDelay) / 2f + MinSimDelay;
        private static readonly Random rand = new();

        private static float AverageDelay(float currentDelay) => delay > 0 ? delay = delay * 0.5f + currentDelay * 0.5f : delay = currentDelay * 0.75f;
        private static float SimulateDelay() => simDelay = Math.Min(Math.Max(simDelay + (float)(rand.NextDouble() - 0.5) * 0.016f, MinSimDelay), MaxSimDelay);
        public static void CompensateAnimationLock(float oldLock, float newLock)
        {
            // Ignore cast locks (caster tax, teleport, lb)
            if (Game.IsCasting || newLock <= 0.11f) // Unfortunately this isn't always true for casting if the user is above 500 ms ping
            {
                if (Config.EnableLogging)
                    PrintLog($"Ignored reducing server cast lock of {F2MS(newLock)} ms");
                return;
            }

            // Special case to (mostly) prevent accidentally using XivAlexander at the same time
            if (!Config.EnableDryRun && newLock % 0.01 is >= 0.0005f and <= 0.0095f)
            {
                ignoreNext = 2;
                PrintError($"Unexpected lock of {F2MS(newLock)} ms");
                return;
            }

            if (ignoreNext > 0)
            {
                ignoreNext--;
                PrintError("Detected possible use of XivAlexander");
                return;
            }

            var responseTime = Game.DefaultClientAnimationLock - oldLock;
            var reduction = Math.Min(AverageDelay(responseTime), responseTime);
            var delayOverride = Math.Min(Math.Max(newLock - reduction + SimulateDelay(), 0), newLock);

            if (!Config.EnableDryRun)
                Game.AnimationLock = delayOverride;

            if (!Config.EnableLogging && oldLock != 0) return;

            var spikeDelay = responseTime - reduction;
            PrintLog($"{(Config.EnableDryRun ? "[DRY] " : string.Empty)}" +
                $"Response: {F2MS(responseTime)} ({F2MS(delay)}) > {F2MS(simDelay + spikeDelay)} ({F2MS(simDelay)} + {F2MS(spikeDelay)}) ms" +
                $"{(Config.EnableDryRun && newLock <= 0.6f && newLock % 0.01 is >= 0.0005f and <= 0.0095f ? $" [Alexander: {F2MS(responseTime - (0.6f - newLock))} ms]" : string.Empty)}" +
                $" || Lock: {F2MS(newLock)} > {F2MS(delayOverride)} ({F2MS(delayOverride - newLock) - 1}) ms");
        }
    }
}
