using System;
using System.Collections;
using UnityEngine;
using Screen = UnityEngine.Device.Screen;

public class CameraTouchController : MonoBehaviour
{
    [Header("Pan")]
    public int panningSpeed = 1;
    public float panThreshold = 0.2f;
    public bool boundCamera = false;
    
    [Tooltip("Bounds width and height in Units.")]
    public Vector2 boundsSize = Vector2.zero;
    public Vector3 boundsCenter = Vector3.zero;
    
    [Tooltip("How many Units you want bounds to shrink.")]
    public float insideOffset = 0;
    
    [Header("Zoom")]
    public bool allowZooming = false;
    public int zoomingSpeed = 1;
    public float zoomThreshold = 0.2f;
    public float defaultZoom = 7f;
    public float maxZoomIn = 4.5f;
    public float maxZoomOut = 15f;
    
    private IEnumerator _coEasingMove;
    private bool _isCoEasingMoveRunning = false;
    private bool _isReachBounds = false;
    
    private Camera _mainCamera;
    private bool _isTouching;
    private bool _isPanning;
    private int _preFingerId = 0;
    private Vector3 _initialTouchPosition;
    private Vector3 _curTouchPosition;
    private Vector3 _lastDirection;
    private float _thresholdCounter;
    private float _panAcceleration = 1f;
    private Vector3 _screenSize;
    private Vector3 _screenSizeOffset;

    private bool _isZooming;
    private float _initialTouchMagnitude;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null) throw new NullReferenceException();
    }

    private void Start()
    {
        Debug.Log(_mainCamera.aspect);
        
        if (boundCamera)
        {
            var orthographicSize = _mainCamera.orthographicSize;
            _screenSize = new Vector3(orthographicSize * _mainCamera.aspect * 2, orthographicSize * 2, 0);
            _screenSizeOffset = _screenSize - new Vector3(insideOffset * 2, insideOffset * 2, 0);
            if (boundCamera) _mainCamera.transform.position = GetValidBounds(_mainCamera.transform.position);
        }
        
        if (allowZooming) _mainCamera.orthographicSize = defaultZoom;
        
    }

    private void Update()
    {
        int touchCount = Input.touchCount;
        if (touchCount > 0) _isTouching = true;
        if (touchCount == 1)
        {
            _isPanning = true;
            _isZooming = false;
        }
        if (touchCount == 2 && allowZooming)
        {
            _isPanning = false;
            _isZooming = true;
        }
        if (touchCount >= 3) return;
        
        if (_isReachBounds && _isCoEasingMoveRunning) StopCoroutine(_coEasingMove);
        
        if (_isTouching)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began || _preFingerId != touch.fingerId)
            {
                if (_isCoEasingMoveRunning) StopCoroutine(_coEasingMove);
                _initialTouchPosition = _mainCamera.ScreenToWorldPoint(touch.position);
                _thresholdCounter = 0f;
                _preFingerId = touch.fingerId;
            }
            
            if(_isPanning)
            {
                Pan(touch);
            }
            
            if (_isZooming)
            {
                Touch touch2 = Input.GetTouch(1);
                Zoom(touch, touch2);
            }
            
            _isTouching = false; 
        }
    }
    
    /// <summary>
    /// Panning
    /// </summary>
    /// <param name="touch"></param>
    private void Pan(Touch touch)
    {
        _curTouchPosition = _mainCamera.ScreenToWorldPoint(touch.position);
        Vector3 direction = _initialTouchPosition - _curTouchPosition;
        
        if (touch.phase == TouchPhase.Moved)
        {
            //Check finger's movement whether reaches threshold.
            _thresholdCounter += direction.magnitude;
            if (Mathf.Abs(_thresholdCounter) >= panThreshold)
            {
                var camTransform = _mainCamera.transform;
                var camPos = camTransform.position;
                camPos += new Vector3(direction.x, direction.y, 0f) * panningSpeed;
                camTransform.position = boundCamera ? GetValidBounds(camPos) : camPos;
            }
            _lastDirection = Vector3.Normalize(direction);
            _panAcceleration = touch.deltaPosition.magnitude / touch.deltaTime;
        }

        
        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
        {
            // use coroutine to perform the easing movement
            _coEasingMove = EasingMove(_panAcceleration, _lastDirection);
            StartCoroutine(_coEasingMove);
        }
    }

    /// <summary>
    /// When the finger leaves screen, starts this coroutine to make a automatic easing camera movement 
    /// </summary>
    /// <param name="speed">Panning speed</param>
    /// <param name="dir">Movement direction</param>
    /// <returns></returns>
    IEnumerator EasingMove(float speed, Vector3 dir)
    {
        _isCoEasingMoveRunning = true;
        var newSpeed = speed;
        while (newSpeed > 0)
        {
            var v = new Vector3(dir.x, dir.y, 0) * (newSpeed * Time.deltaTime / 500);
            var camPos = _mainCamera.transform.position + v;
            _mainCamera.transform.position = boundCamera ? GetValidBounds(camPos) : camPos;
            yield return null;
            newSpeed -= Time.deltaTime * 8000;
        }
        _isCoEasingMoveRunning = false;
    }
    
    /// <summary>
    /// Validate camera position and return a value that limited in the bounds. 
    /// </summary>
    /// <param name="camPos">Camera position</param>
    /// <param name="isReachBounds">If the camera reach the bounds or not</param>
    /// <returns></returns>
    private Vector3 GetValidBounds(Vector3 camPos)
    {
        _isReachBounds = false;
        var halfScreenSizeX = _screenSizeOffset.x / 2;;
        var halfScreenSizeY = _screenSizeOffset.y / 2;
        var halfBoundsSizeX = boundsSize.x / 2;
        var halfBoundsSizeY = boundsSize.y / 2;
        var leftBounds = boundsCenter.x - halfBoundsSizeX;
        var rightBounds = boundsCenter.x + halfBoundsSizeX;
        var upBounds = boundsCenter.y + halfBoundsSizeY;
        var bottomBounds = boundsCenter.y - halfBoundsSizeY;
        if (camPos.x + halfScreenSizeX > rightBounds)
        {
            camPos.x = rightBounds - halfScreenSizeX;
            _isReachBounds = true;
        }
        
        if (camPos.x - halfScreenSizeX < leftBounds)
        {
            camPos.x = leftBounds + halfScreenSizeX;
            _isReachBounds = true;
        }
        
        if (camPos.y + halfScreenSizeY > upBounds)
        {
            camPos.y = upBounds - halfScreenSizeY;
            _isReachBounds = true;
        }
        
        if (camPos.y - halfScreenSizeY < bottomBounds)
        {
            camPos.y = bottomBounds + halfScreenSizeY;
            _isReachBounds = true;
        }
        return camPos;
    }

    /// <summary>
    /// Zooming
    /// </summary>
    /// <param name="touch1"></param>
    /// <param name="touch2"></param>
    private void Zoom(Touch touch1, Touch touch2)
    {
        var touch1PrevPos = touch1.position - touch1.deltaPosition;
        var touch2PrevPos = touch2.position - touch2.deltaPosition;
        var preMagnitude = (touch1PrevPos - touch2PrevPos).magnitude;
        var curMagnitude = (touch1.position - touch2.position).magnitude;
        var diff = preMagnitude - curMagnitude;
        var currentOrthographicSize = _mainCamera.orthographicSize;;
        
        //Check the distance between two fingers whether reaches threshold.
        _thresholdCounter += diff;
        if (Mathf.Abs(_thresholdCounter) >= zoomThreshold)
        {
            currentOrthographicSize += diff * 0.01f * zoomingSpeed;
            _mainCamera.orthographicSize = currentOrthographicSize;
            if (_mainCamera.orthographicSize < maxZoomIn) _mainCamera.orthographicSize = maxZoomIn;
            if (_mainCamera.orthographicSize > maxZoomOut) _mainCamera.orthographicSize = maxZoomOut;
        }
    }
}
