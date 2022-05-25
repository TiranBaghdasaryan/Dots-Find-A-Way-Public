using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioSource _plopAudio;
    [SerializeField] private AudioSource _winAudio;

    public static void CallAudioClipPlop()
    {

        if (Data.IsSoundOn)
        {
            AudioSource audioObject = Instantiate(Instance._plopAudio);

            Instance.StartCoroutine(localCoroutine());
            IEnumerator localCoroutine()
            {
                yield return new WaitForSeconds(.5f);
                Destroy(audioObject.gameObject);
            }
        }
    }
    
    public static void CallAudioClipWin()
    {

        if (Data.IsSoundOn)
        {
            AudioSource audioObject = Instantiate(Instance._winAudio);

            Instance.StartCoroutine(localCoroutine());
            IEnumerator localCoroutine()
            {
                yield return new WaitForSeconds(1f);
                Destroy(audioObject.gameObject);
            }
        }
    }


    private void Awake()
    {
        Instance = this;
    }

    public static AudioManager Instance;
}