using Test;
using Zenject;

namespace Installers
{
    public class TestSceneInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.Bind<HitParticlePool>().FromComponentInHierarchy().AsSingle().IfNotBound();
        }
    }
}

