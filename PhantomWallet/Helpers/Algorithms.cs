using System;
using System.Collections.Generic;

namespace Phantom.Wallet.Helpers
{
    public static class Algorithms
    {
        public static Func<T, IEnumerable<T>> ShortestPathFunction<T>(Graph<T> graph, T start)
        {
            var previous = new Dictionary<T, T>();

            var queue = new Queue<T>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var vertex = queue.Dequeue();
                foreach (var neighbor in graph.AdjacencyList[vertex])
                {
                    if (previous.ContainsKey(neighbor))
                        continue;

                    previous[neighbor] = vertex;
                    queue.Enqueue(neighbor);
                }
            }

            IEnumerable<T> ShortestPath(T v)
            {
                var path = new List<T>();

                var current = v;
                while (!current.Equals(start))
                {
                    path.Add(current);
                    current = previous[current];
                }

                path.Add(start);
                path.Reverse();

                return path;
            }

            return ShortestPath;
        }
    }
}
