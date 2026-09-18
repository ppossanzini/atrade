import { computed, defineComponent } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'

/**
 * Reusable placeholder so every sidebar entry is always navigable, including the sections that are
 * not implemented yet. The title comes from the route metadata.
 */
export default defineComponent({
  name: 'ComingSoonView',
  setup() {
    const { t } = useI18n()
    const route = useRoute()

    const pageTitleKey = computed(() => String(route.meta.titleKey ?? 'comingSoon.title'))

    return { t, pageTitleKey }
  },
})
