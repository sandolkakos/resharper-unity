using System.Collections.Generic;
using System.Linq;
using JetBrains.ReSharper.Plugins.Unity.Yaml.Psi.Caches.AssetHierarchy.Elements;

namespace JetBrains.ReSharper.Plugins.Unity.Yaml.Psi.Caches.AssetHierarchy
{
    public class UnityScenePathGameObjectConsumer : IGameObjectConsumer
    {
        public List<string> NameParts => myParts.ToList();
        public List<int> RootIndexes => myIndex.ToList();
        
        private readonly Stack<string> myParts = new Stack<string>();
        private readonly Stack<int> myIndex = new Stack<int>();
        
        public bool AddGameObject(GameObjectHierarchy gameObject)
        {
            myParts.Push(gameObject.Name);
            myIndex.Push(gameObject.Transform.RootIndex);
            return true;
        }
    }
}