#if UNITY_2021_3_OR_NEWER
using ArtificialLife.Core;
using UnityEngine;

namespace ArtificialLife.Samples
{
    public sealed class UnitySettlementBehaviour : MonoBehaviour
    {
        private readonly SettlementSession session = new SettlementSession();
        private void Update()
        {
            foreach (SettlementEvent item in session.Update(Time.deltaTime))
                if (item is SettlementFounded founded) Debug.Log($"Settlement founded: {founded.SettlementId}");
        }
        public void SaveGame(string path) => System.IO.File.WriteAllText(path, JsonUtility.ToJson(new SaveEnvelope(session.Save())));
        public void LoadGame(string path) => session.Load(JsonUtility.FromJson<SaveEnvelope>(System.IO.File.ReadAllText(path)).ToSave());
        [System.Serializable]
        private sealed class SaveEnvelope
        {
            public int schema; public string kind; public string payload; public string checksum;
            public SaveEnvelope(SimulationSave save) { schema = save.Schema; kind = save.Kind; payload = System.Convert.ToBase64String(save.Payload); checksum = save.Checksum; }
            public SimulationSave ToSave() => new SimulationSave { Schema = schema, Kind = kind, Payload = System.Convert.FromBase64String(payload), Checksum = checksum };
        }
    }
}
#endif
