// TrafficManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TrafficManager : MonoBehaviour
{
    // SpawnLoop 제어용 플래그
    private bool spawningEnabled = true;
    
    [Header("Player Reference")]
    public Transform player;

    [Header("Prefabs & Pooling")]
    public List<GameObject> vehiclePrefabs;
    public int initialPoolSize = 10;
    public Transform aheadVehicleTransform;
    [Header("Spawn Settings")]
    public float spawnDistanceAhead    = 100f;
    public float despawnDistanceBehind = 50f;
    public float minSpawnInterval      = 0.5f;
    public float maxSpawnInterval      = 2f;

    [Header("Speed Range (km/h)")]
    public float minSpeedKmh = 30f;
    public float maxSpeedKmh = 80f;

    [Header("Lane X Positions")]
    public float[] laneXPositions;

    [Header("Lane Gap Settings")]
    public float laneGap = 10f;

    // 풀 & 활성 차량 목록
    private Queue<GameObject> pool = new Queue<GameObject>();
    private List<GameObject> activeVehicles = new List<GameObject>();

    // 차선별 마지막 스폰 Z 위치
    private float[] lastSpawnZPerLane;
    // 차선별 마지막 차량 레퍼런스
    private Dictionary<int, TrafficVehicle> lastVehiclePerLane = new Dictionary<int, TrafficVehicle>();
    // 차량 → 차선 매핑
    private Dictionary<GameObject, int> vehLaneMap = new Dictionary<GameObject, int>();

    void Start()
    {
        // 풀 미리 채우기
        for (int i = 0; i < initialPoolSize; i++)
        {
            var go = Instantiate(vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)]);
            go.SetActive(false);
            pool.Enqueue(go);
        }

        // 차선별 마지막 Z 초기화 (플레이어 기준)
        lastSpawnZPerLane = new float[laneXPositions.Length];
        for (int i = 0; i < lastSpawnZPerLane.Length; i++)
            lastSpawnZPerLane[i] = player.position.z;

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minSpawnInterval, maxSpawnInterval));

            if (spawningEnabled)  // 스폰이 활성화된 경우에만
                SpawnVehicle();
        }
    }

    /// <summary>
    /// 호출 시 차량 스폰을 중지하고,
    /// activeVehicles에 있는 모든 차량을 뒤로 밀고 속도를 깎는다.
    /// </summary>
    /// <param name="speedReductionKmh">감소시킬 속도(km/h)</param>
    public void PauseSpawnAndPushBack(float speedReductionKmh)
    {
        Debug.Log("다른 차선 차량 스폰 중지, 속도 감소");
        // 1) 더 이상 스폰하지 않는다
        spawningEnabled = false;

        // 2) 모든 활성 차량에 동일한 효과 적용
        foreach (var vehObj in activeVehicles)
        {
            // 속도 감소
            var tv = vehObj.GetComponent<TrafficVehicle>();
            if (tv != null)
            {
                float newSpeed = tv.GetSpeed() - speedReductionKmh;
                tv.SetSpeed(Mathf.Max(0f, newSpeed)); // 음수 속도 방지
            }
        }
    }

    /// <summary>
    /// 호출 시 다시 스폰을 재개한다.
    /// </summary>
    public void ResumeSpawn()
    {
        spawningEnabled = true;
    }


    private void SpawnVehicle()
    {
        int lane = player.position.x < 2 ? 1 : 0; 
        float x = laneXPositions[lane];

        // 2) 해당 차선에서 가장 앞에 있는 활성 차량의 Z 위치 계산
        float leadingVehicleZ = player.position.z;
        foreach (var veh in activeVehicles)
        {
            if (vehLaneMap.TryGetValue(veh, out int vehLane) && vehLane == lane)
            {
                float zPos = veh.transform.position.z;
                if (zPos > leadingVehicleZ)
                    leadingVehicleZ = zPos;
            }
        }

        // 3) 스폰 기준 Z 계산 (플레이어 앞 spawnDistanceAhead vs. 앞차 + minLaneGap)
        float playerZBase = player.position.z + spawnDistanceAhead;
        float laneBaseZ   = leadingVehicleZ + spawnDistanceAhead;
        float baseZ       = Mathf.Max(playerZBase, laneBaseZ);
        float z        = baseZ;
        if(100 <  z - player.position.z) return;
        
        // 5) 풀에서 차량 가져오기 또는 새 인스턴스 생성
        GameObject vehObj;
        if (pool.Count > 0)
        {
            vehObj = pool.Dequeue();
            vehObj.SetActive(true);
        }
        else
        {
            vehObj = Instantiate(vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)]);
        }

        // 6) 위치·회전 초기화
        vehObj.transform.position = new Vector3(x, vehObj.transform.position.y, z);
        aheadVehicleTransform = vehObj.transform; 
        // vehObj.transform.rotation = Quaternion.identity;

        // 7) TrafficVehicle 컴포넌트 세팅
        var tv = vehObj.GetComponent<TrafficVehicle>() ?? vehObj.AddComponent<TrafficVehicle>();
        tv.SetLane(lane);

        // 8) 속도 결정 (앞차 속도 ×1.1 이내, 최대 maxSpeedKmh)
        float maxAllowed = maxSpeedKmh;
        if (lastVehiclePerLane.TryGetValue(lane, out var frontTv))
        {
            maxAllowed = Mathf.Min(maxAllowed, frontTv.GetSpeed() * 1.1f);
        }
        float speed = maxAllowed;
        tv.SetSpeed(speed);

        // 9) 관리 컬렉션 업데이트
        lastVehiclePerLane[lane] = tv;
        vehLaneMap[vehObj]       = lane;
        activeVehicles.Add(vehObj);
    }

    void Update()
    {
        // 뒤로 벗어난 차량 리사이클
        for (int i = activeVehicles.Count - 1; i >= 0; i--)
        {
            var veh = activeVehicles[i];
            if (veh.transform.position.z < player.position.z - despawnDistanceBehind)
            {
                // 차선 정보 정리
                int lane = vehLaneMap[veh];
                if (lastVehiclePerLane.TryGetValue(lane, out var lastTv) && lastTv.gameObject == veh)
                    lastVehiclePerLane.Remove(lane);
                vehLaneMap.Remove(veh);

                // 리스트·풀 업데이트
                activeVehicles.RemoveAt(i);
                veh.SetActive(false);
                pool.Enqueue(veh);
            }
        }
    }
}
