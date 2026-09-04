using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace MoonWorld
{
    // Individual sources propose deltas here; the cycle applies them only at phase boundaries.
    public sealed class PranaLedger
    {
        private readonly Dictionary<Need, float> deltas = new Dictionary<Need, float>();

        public float LevelAfterPending(Need need)
        {
            if (need == null)
            {
                return 0f;
            }

            float delta;
            deltas.TryGetValue(need, out delta);
            return Mathf.Clamp(need.CurLevel + delta, 0f, need.MaxLevel);
        }

        public float RemainingCapacity(Need need)
        {
            return need == null ? 0f : need.MaxLevel - LevelAfterPending(need);
        }

        public void Add(Need need, float amount)
        {
            if (need == null || Mathf.Approximately(amount, 0f))
            {
                return;
            }

            float existing;
            deltas.TryGetValue(need, out existing);
            deltas[need] = existing + amount;
        }

        public void Commit()
        {
            foreach (KeyValuePair<Need, float> entry in deltas)
            {
                Need need = entry.Key;
                if (need != null)
                {
                    need.CurLevel = Mathf.Clamp(need.CurLevel + entry.Value, 0f, need.MaxLevel);
                }
            }
            deltas.Clear();
        }
    }
}
