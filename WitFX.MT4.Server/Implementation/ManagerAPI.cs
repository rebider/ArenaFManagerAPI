using System;
using System.Diagnostics;

namespace WitFX.MT4.Server.Implementation
{
    public sealed class ManagerAPI : IDisposable
    {
        public CManagerInterface m_PumpMgr;
        public CManagerInterface m_Mgr;
        public CManagerInterface m_PumpMgrEx;
        public CManagerInterface m_DBMgr;

        private CManagerFactory m_factory;
        private bool m_isValid;

        public ManagerAPI(CManagerFactory factory)
        {
            m_factory = factory ?? throw new ArgumentNullException(nameof(factory));
            if (m_factory.IsValid() == false ||
                (m_PumpMgr = m_factory.Create()) == null ||
                (m_Mgr = m_factory.Create()) == null ||
                (m_PumpMgrEx = m_factory.Create()) == null)
            {
                m_isValid = false;
                return;
            }
            m_isValid = true;
            Debug.Assert(m_isValid);
        }

        public void createDBMT4Manager()
        {
            Debug.Assert(m_DBMgr == null);
            m_DBMgr = m_factory.Create();
        }

        public void Dispose()
        {
            if (m_Mgr != null)
            {
                m_Mgr.Dispose();
                m_Mgr = null;
            }
            if (m_DBMgr != null)
            {
                m_DBMgr.Dispose();
                m_DBMgr = null;
            }
            if (m_PumpMgr != null)
            {
                m_PumpMgr.Dispose();
                m_PumpMgr = null;
            }
            if (m_PumpMgrEx != null)
            {
                m_PumpMgrEx.Dispose();
                m_PumpMgrEx = null;
            }

            if (m_factory != null)
            {
                //m_factory.Dispose();
                m_factory = null;
            }
        }

        //====================================================================
        void ReleaseManager()
        {
            if (m_Mgr != null)
            {
                if (m_Mgr.IsConnected())
                {
                    m_Mgr.Disconnect();
                }
                m_Mgr.Dispose();
            }
            m_Mgr = null;
        }
        //====================================================================
        void ReleasePumpManager()
        {
            if (m_PumpMgr != null)
            {
                if (m_PumpMgr.IsConnected())
                {
                    m_PumpMgr.Disconnect();
                }
                m_PumpMgr.Dispose();
            }
            m_PumpMgr = null;
        }
        //====================================================================
        void ReleasePumpManagerEx()
        {
            if (m_PumpMgrEx != null)
            {
                if (m_PumpMgrEx.IsConnected())
                {
                    m_PumpMgrEx.Disconnect();
                }
                m_PumpMgrEx.Dispose();
            }
            m_PumpMgrEx = null;
        }
        //====================================================================
        public bool isPumpManagerExValid()
        {
            if (m_factory.IsValid() && m_PumpMgrEx != null)
            {
                return true;
            }
            return false;
        }
        //====================================================================
        public bool isPumpManagerValid()
        {
            if (m_factory.IsValid() && m_PumpMgr != null)
            {
                return true;
            }
            return false;
        }
        //====================================================================
        public bool isNormalManagerValid()
        {
            if (m_factory.IsValid() && m_Mgr != null)
            {
                return true;
            }
            return false;
        }
        //====================================================================
        public bool isAPIValid()
        {
            if (m_factory.IsValid() && m_isValid)
            {
                return true;
            }
            return false;
        }
    }
}
