using UnityEngine;
using UnityEngine.Events;

namespace InventorySystem
{
    /// <summary>
    /// Makes a GameObject give gold to the player - the inventory's general gold, which belongs to no item group. Like
    /// <see cref="Collectible"/> but for gold: drag it into an <c>Interactable</c>'s <c>On Interact</c> and pick
    /// <c>Collect</c>. The amount is either fixed or random between a minimum and a maximum.
    /// </summary>
    /// <remarks>
    /// <b>On Collected</b> fires once the gold is added (adding the component wires <c>GameObject.SetActive(false)</c> to it
    /// by default, so the object leaves the world). With PoolSystem in the project, an object taken from a pool also goes
    /// back to it once collected - don't wire <c>Poolable &gt; ReleaseSelf</c> as well. The object can be collected once
    /// per time it is enabled, so a pooled one comes back full.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GoldCollectible : MonoBehaviour
    {
        /// <summary>The largest value the Inspector's Min / Max slider reaches.</summary>
        public const int MaxSliderGold = 1000;

        [Tooltip("Off: the object always gives Amount. On: it gives a random amount between Min and Max each time it is collected.")]
        [SerializeField] private bool randomAmount;

        [Tooltip("How much gold the object gives.")]
        [Min(0)] [SerializeField] private int amount = 10;

        [Tooltip("The least (x) and the most (y) gold the object can give, both included.")]
        [SerializeField] private Vector2 range = new(5f, 20f);

        [Tooltip("Invoked once the gold was added. Remove the object from the world here: GameObject > SetActive(false), " +
                 "or Poolable > ReleaseSelf for pooled objects.")]
        [SerializeField] private UnityEvent onCollected = new();

        private bool collected;

        /// <summary>Whether the gold is a random amount between <see cref="MinAmount"/> and <see cref="MaxAmount"/>.</summary>
        public bool RandomAmount => randomAmount;

        /// <summary>The least gold the object gives: its fixed amount, or the random range's lower end.</summary>
        public int MinAmount => randomAmount ? Mathf.Max(0, Mathf.RoundToInt(Mathf.Min(range.x, range.y))) : amount;

        /// <summary>The most gold the object gives: its fixed amount, or the random range's upper end.</summary>
        public int MaxAmount => randomAmount ? Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(range.x, range.y))) : amount;

        /// <summary>The gold the last <see cref="Collect"/> gave.</summary>
        public int LastAmount { get; private set; }

        private void OnEnable() => collected = false;

        /// <summary>Gives the gold to the player. For UnityEvents (e.g. an Interactable's On Interact). Does nothing after the first call until the object is enabled again.</summary>
        public void Collect()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            LastAmount = Roll();
            InventoryManager.AddGold(LastAmount);
            onCollected.Invoke();
#if HAS_POOL_SYSTEM
            // Taken from a pool: back to it. An object placed in the scene isn't, and stays as On Collected left it.
            PoolSystem.PoolManager.TryRelease(gameObject);
#endif
        }

        /// <summary>The gold a collect gives right now: the fixed amount, or a new random one between <see cref="MinAmount"/> and <see cref="MaxAmount"/>.</summary>
        public int Roll() => randomAmount ? Random.Range(MinAmount, MaxAmount + 1) : amount;

#if UNITY_EDITOR
        // A collected object leaves the world by default - as a Collectible does; visible and changeable in On Collected.
        private void Reset()
        {
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(onCollected, gameObject.SetActive, false);
        }
#endif
    }
}
