using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CutsceneManager : MonoBehaviour
{
    public GameObject[] comicPages;
    public float fadeDuration = 1.5f;
    public Button nextButton;
    private List<Graphic> comicPanels = new List<Graphic>();
    private int numberOfPage;
    private int currentPage = 1;

    void Start()
    {
        numberOfPage = comicPages.Count();
        StartCoroutine(PlayCutscene());
    }

    private IEnumerator PlayCutscene()
    {
        comicPanels.Clear();
        comicPages[currentPage - 1].SetActive(true);
        for (var i = 0; i < comicPages[currentPage - 1].transform.childCount; i++)
        {
            comicPanels.Add(
                comicPages[currentPage - 1].transform.GetChild(i).GetChild(0).GetComponent<Graphic>()
            );
        }

        foreach (var panel in comicPanels)
        {
            panel.CrossFadeAlpha(0, 0, true);
        }
        nextButton.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.5f);

        foreach (var panel in comicPanels)
        {
            panel.CrossFadeAlpha(1, fadeDuration, true);

            yield return new WaitForSeconds(fadeDuration);
        }

        nextButton.gameObject.SetActive(true);
        nextButton.onClick.AddListener(OnNextButtonPressed);
    }

    // Update is called once per frame
    public void OnNextButtonPressed()
    {
        Debug.Log("Next button pressed! Loading next scene or comic page...");

        if (currentPage < numberOfPage)
        {
            comicPages[currentPage - 1].SetActive(false);
            currentPage += 1;
            StartCoroutine(PlayCutscene());
        }
        else
        {
            if (SceneManager.GetActiveScene().name == "Prolog")
            {
                SceneManager.LoadScene("BaseScene");
            }
            else
            {
                SceneManager.LoadScene("MainMenu");
            }
            //Load Scene
            Debug.Log("Cutescene Done");
        }
    }
}
