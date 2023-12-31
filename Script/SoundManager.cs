using Sirenix.OdinInspector;
using System.Collections.Generic;
using Toolbox;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : Singleton<SoundManager>
{
    [SerializeField] List<SoundBusInfo> soundBusInfoList = null;
    [SerializeField] List<SoundGroup> soundGroupsListSFX = null;
    [SerializeField] List<SoundGroup> soundGroupsListUI = null;
    [SerializeField] List<SoundGroup> soundGroupsListVoice = null;
    
    private readonly Dictionary<string, SoundGroup> soundGroups = new();
    private readonly Dictionary<SoundBUS, SoundBusInfo> allBusInfo = new();

    protected override void Awake()
    {
        base.Awake();

        foreach (SoundBusInfo info in soundBusInfoList)
        {
            allBusInfo[info.soundBus] = info;
        }
    }

    private void Start()
    {
        List<SoundGroup> allSoundGroups = new();
        allSoundGroups.AddRange(soundGroupsListSFX);
        allSoundGroups.AddRange(soundGroupsListUI);
        allSoundGroups.AddRange(soundGroupsListVoice);

        foreach (SoundGroup soundGroup in allSoundGroups)
        {
            soundGroups[soundGroup.gameObject.name] = soundGroup;
        }
    }

    private void PlaySoundInternal(string soundGroupName, Transform location)
    {
        if (soundGroups.TryGetValue(soundGroupName, out SoundGroup soundGroup))
        {
            SoundBusInfo busInfo = allBusInfo[soundGroup.SoundBUS];

            if (busInfo.activeSources.Count >= busInfo.voiceLimit && busInfo.activeSources.Count > 0)
            {
                AudioSource activeSource = busInfo.activeSources[0];
                SoundGroup activeSourceSoundGroup = busInfo.activeSourcesSoundGroup[0];
                activeSourceSoundGroup.Stop(activeSource);
            }

            (AudioSource source, SoundGroup sourceSoundGroup) = soundGroup.GetAvailableSource();

            if (source != null)
            {
                busInfo.activeSources.Add(source);
                busInfo.activeSourcesSoundGroup.Add(sourceSoundGroup);

                if (location != null)
                {
                    source.transform.position = location.position;
                }

                source.Play();
            }
            else
            {
                Debug.LogWarning("Available audio source not found. Sound wasn't played: " + soundGroupName);
            }
        }
    }

    public void HandleAudioSourceStopped(SoundGroup soundGroup, AudioSource src)
    {
        SoundBUS bus = soundGroup.SoundBUS;
        SoundBusInfo busInfo = allBusInfo[bus];

        if (busInfo.activeSources.Count > 0)
        {
            busInfo.activeSources.Remove(src);
            busInfo.activeSourcesSoundGroup.Remove(soundGroup);
        }
        else
        {
            Debug.LogWarning("HandleAudioSourceStopped was invoked, but we have no actives voice.");
        }
    }

    public void PlaySound(string soundGroupName)
    {
        PlaySoundInternal(soundGroupName, null);
    }

    public void PlaySound(string soundGroupName, Transform location)
    {
        PlaySoundInternal(soundGroupName, location);
    }

    public void SetBusVolume(SoundBUS bus, float volume)
    {
        // TODO: This method is untested.
        SoundBusInfo control = allBusInfo[bus];
        control.individualVolume = volume;
        control.audioMixer.SetFloat("Volume", Mathf.Log10(volume) * 20);
    }

    #region Editor

#if UNITY_EDITOR

    public SoundBusInfo GetBUSInfoFromList(SoundBUS bus)
    {
        foreach (SoundBusInfo info in soundBusInfoList)
        {
            if (info.soundBus == bus)
            {
                return info;
            }
        }

        return null;
    }

    [Title("Editor-time helper", "For batch creation of sound groups", TitleAlignments.Centered)]
    [InfoBox("Sound groups are pools that contain variations of a sound. Each sound group is represented by a folder that contains AudioClip variations.\n\n " +
        "Usage:\n" +
        "1) Click \"Lock Inspector\" while the scene game object with the SoundManager.cs component is selected.\n" +
        "2) Select multiple sound group folders in the Project view.\n" +
        "3) Click this button.\n\n" +
        "For each selected folder, a new game object with the same name will be created with SoundGroup.cs attached. To each of these game objects, we create child game objects with AudioSource components attached. Default settings for the AudioSource components will be set.")]
    [SerializeField] AudioMixerGroup audioMixerGroupForBatch = null;
    [SerializeField] bool use3DSoundForBatch = false;
    [Button("Create game objects for sound groups")]
    public void CreateChildObjects()
    {
        Object[] selectedAssets = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);

        // Sort by name
        //selectedAssets = selectedAssets.OrderBy(asset => asset.name).ToArray();
        System.Array.Sort(selectedAssets, (a, b) => string.Compare(a.name, b.name));

        foreach (Object asset in selectedAssets)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.Log("Invalid asset.");
                continue;
            }

            // Create sound group object
            GameObject soundGroupObject = new("sfx_" + asset.name);
            SoundGroup soundGroup = soundGroupObject.AddComponent<SoundGroup>();
            soundGroupObject.transform.SetParent(transform);

            // Create audio source objects
            string[] assetPaths = AssetDatabase.FindAssets("t:AudioClip", new[] { assetPath });
            foreach (string assetGUID in assetPaths)
            {
                string childAssetPath = AssetDatabase.GUIDToAssetPath(assetGUID);
                string childAssetName = System.IO.Path.GetFileNameWithoutExtension(childAssetPath);

                AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(childAssetPath);

                // Create empty child game objects
                GameObject audioSourceObject = new(childAssetName);
                audioSourceObject.transform.SetParent(soundGroupObject.transform);
                                
                AudioSource audioSource = audioSourceObject.AddComponent<AudioSource>();

                audioSource.clip = audioClip;
                audioSource.outputAudioMixerGroup = audioMixerGroupForBatch;
                audioSource.loop = false;
                audioSource.playOnAwake = false;
                audioSource.volume = 1.0f;
                audioSource.pitch = 1.0f;
                audioSource.spatialBlend = use3DSoundForBatch ? 1.0f : 0.0f;

                if (soundGroup == null)
                {
                    Debug.Log("Why");
                }
                soundGroup.AudioSources.Add(audioSource);
            }
        }
    }
#endif

    #endregion

}

