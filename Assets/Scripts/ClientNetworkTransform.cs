using Unity.Netcode.Components;

// プレイヤー本人(オーナー)が自分の位置を決める NetworkTransform。
// これがないと、クライアントの移動がホストに反映されない。
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
