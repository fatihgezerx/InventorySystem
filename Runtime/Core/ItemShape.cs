using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>The cells an item covers in a <see cref="InventoryType.Grid"/> inventory.</summary>
    public enum ItemShape
    {
        /// <summary>1 cell.</summary>
        [InspectorName("Square 1x1 (1 slot)")] Square1x1,

        /// <summary>2 cells side by side.</summary>
        [InspectorName("Rectangle 2x1 (2 slots)")] Rectangle2x1,

        /// <summary>3 cells in an L inside a 2x2 box: <c>X. / XX</c>.</summary>
        [InspectorName("L Shape (3 slots)")] LShape,

        /// <summary>3 cells side by side.</summary>
        [InspectorName("Rectangle 3x1 (3 slots)")] Rectangle3x1,

        /// <summary>4 cells in a 2x2 square.</summary>
        [InspectorName("Square 2x2 (4 slots)")] Square2x2
    }

    /// <summary>
    /// Precomputed cell layouts of every <see cref="ItemShape"/> in every rotation. A rotation is a
    /// number of clockwise quarter turns (0-3); cells are offsets from the item's top-left cell, with
    /// x growing right and y growing down - the same way a UI grid is laid out.
    /// </summary>
    public static class ItemShapes
    {
        private static readonly Vector2Int[][][] CellTable;
        private static readonly Vector2Int[][] SizeTable;

        // How many rotations of each shape actually differ, in ItemShape order.
        private static readonly int[] RotationCounts = { 1, 2, 4, 2, 1 };

        static ItemShapes()
        {
            var baseShapes = new[]
            {
                new[] { new Vector2Int(0, 0) },
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) },
                new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) }
            };

            CellTable = new Vector2Int[baseShapes.Length][][];
            SizeTable = new Vector2Int[baseShapes.Length][];

            for (var shape = 0; shape < baseShapes.Length; shape++)
            {
                CellTable[shape] = new Vector2Int[4][];
                SizeTable[shape] = new Vector2Int[4];

                var cells = baseShapes[shape];
                for (var rotation = 0; rotation < 4; rotation++)
                {
                    var size = Measure(cells);
                    CellTable[shape][rotation] = cells;
                    SizeTable[shape][rotation] = size;
                    cells = RotateClockwise(cells, size.y);
                }
            }
        }

        /// <summary>How many rotations of <paramref name="shape"/> differ: 1 for squares, 2 for rectangles, 4 for the L.</summary>
        public static int GetRotationCount(ItemShape shape) => RotationCounts[(int)shape];

        /// <summary>
        /// Maps any rotation to its distinct equivalent, e.g. a rectangle's 2 (180°) to 0 - so a UI never
        /// draws a symmetric item upside down.
        /// </summary>
        public static int NormalizeRotation(ItemShape shape, int rotation)
        {
            var quarterTurns = rotation % 4;
            if (quarterTurns < 0)
            {
                quarterTurns += 4;
            }

            return quarterTurns % GetRotationCount(shape);
        }

        /// <summary>The cells <paramref name="shape"/> covers when turned <paramref name="rotation"/> times clockwise.</summary>
        public static IReadOnlyList<Vector2Int> GetCells(ItemShape shape, int rotation) => GetCellArray(shape, rotation);

        /// <summary>The width (x) and height (y), in cells, of <paramref name="shape"/>'s bounding box in that rotation.</summary>
        public static Vector2Int GetSize(ItemShape shape, int rotation) => SizeTable[(int)shape][rotation & 3];

        /// <summary>How many cells <paramref name="shape"/> covers.</summary>
        public static int GetCellCount(ItemShape shape) => CellTable[(int)shape][0].Length;

        internal static Vector2Int[] GetCellArray(ItemShape shape, int rotation) => CellTable[(int)shape][rotation & 3];

        private static Vector2Int Measure(Vector2Int[] cells)
        {
            var size = Vector2Int.zero;
            foreach (var cell in cells)
            {
                size.x = Mathf.Max(size.x, cell.x + 1);
                size.y = Mathf.Max(size.y, cell.y + 1);
            }

            return size;
        }

        // A quarter turn clockwise with y pointing down: (x, y) -> (height - 1 - y, x).
        private static Vector2Int[] RotateClockwise(Vector2Int[] cells, int height)
        {
            var rotated = new Vector2Int[cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                rotated[i] = new Vector2Int(height - 1 - cells[i].y, cells[i].x);
            }

            return rotated;
        }
    }
}
