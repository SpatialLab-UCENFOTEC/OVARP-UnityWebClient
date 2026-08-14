using UnityEngine;

namespace EmotionalTracking
{
    [System.Serializable]
    public enum SemanticTagType {
        Interactive,
        Environment,
        Character
    }

    public class SemanticTag : MonoBehaviour
    {
        public SemanticTagType tag;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            
        }

        public string GetTag() {
            switch (tag) {
                case SemanticTagType.Interactive:
                    return "interactive_entity";
                case SemanticTagType.Character:
                    return "character_entity";
                case SemanticTagType.Environment:
                    return "environment_entity";
                default:
                    return "none_entity";
            }
        }
    }
}