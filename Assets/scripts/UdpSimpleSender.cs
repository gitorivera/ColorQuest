using System.Net.Sockets;
using UnityEngine;

public class UdpSender : MonoBehaviour
{
    public static UdpSender Instance;

    public enum ConnectionMode { WiFi, Link }

    [SerializeField] ConnectionMode mode = ConnectionMode.Link;
    [SerializeField] string wifiIP = "192.168.1.100";
    [SerializeField] int port = 9999;

    string HostIP => mode == ConnectionMode.Link ? "10.0.2.2" : wifiIP;

    // Byte codes for each game event
    public const byte CODE_SPHERE_CORRECT   = 0x01;
    public const byte CODE_SPHERE_INCORRECT = 0x02;
    public const byte CODE_SEQ_COMPLETED    = 0x03;
    public const byte CODE_LEVEL_START      = 0x04;
    public const byte CODE_GAME_END         = 0x05;
    public const byte CODE_BOARD_SPAWN      = 0x06;

    UdpClient udp;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            udp = new UdpClient();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Send(byte code)
    {
        try
        {
            byte[] data = { code };
            udp.Send(data, data.Length, HostIP, port);
            Debug.Log($"[UDP] Sent 0x{code:X2} to {HostIP}:{port}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UDP] Send failed: {e.Message}");
        }
    }

    void OnDestroy()
    {
        udp?.Close();
    }
}